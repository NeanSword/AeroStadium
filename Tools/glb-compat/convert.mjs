import { writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import draco3d from 'draco3dgltf';
import sharp from 'sharp';
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';

const args = process.argv.slice(2);
const batch = args[0] === '--batch';
if ((!batch && args.length !== 2) || (batch && args.length !== 3)) {
    throw new Error('Usage: node convert.mjs input.glb output.glb | --batch sourceDir resourcesDir');
}

const io = new NodeIO()
    .registerExtensions(ALL_EXTENSIONS)
    .registerDependencies({
        'draco3d.decoder': await draco3d.createDecoderModule(),
    });

async function convertOne(inputArg, outputArg) {
    const input = resolve(inputArg);
    const output = resolve(outputArg);
    const document = await io.read(input);
    const root = document.getRoot();
    const extensionNamesBefore = root.listExtensionsUsed().map((extension) => extension.extensionName);
    let convertedTextures = 0;
    let uvChannelsRemapped = 0;

    for (const texture of root.listTextures()) {
        if (texture.getMimeType() !== 'image/webp') continue;
        const image = texture.getImage();
        if (!image) throw new Error('Texture has no image bytes: ' + texture.getName());
        const png = await sharp(Buffer.from(image)).png().toBuffer();
        texture.setImage(new Uint8Array(png));
        texture.setMimeType('image/png');
        convertedTextures++;
    }

    // Unity's URP glTFast material importer supports texture coordinates 0–1.
    // Some exported models include a higher UV channel that exactly duplicates
    // one of those supported channels. Remap only those proven duplicates;
    // fail explicitly when an unsupported channel contains unique coordinates.
    const materialPrimitives = new Map(root.listMaterials().map((material) => [material, []]));
    for (const mesh of root.listMeshes()) {
        for (const primitive of mesh.listPrimitives()) {
            const material = primitive.getMaterial();
            if (materialPrimitives.has(material)) materialPrimitives.get(material).push(primitive);
        }
    }
    const textureInfoGetters = [
        'getBaseColorTextureInfo', 'getMetallicRoughnessTextureInfo',
        'getNormalTextureInfo', 'getOcclusionTextureInfo', 'getEmissiveTextureInfo',
    ];
    const coordinatesMatch = (source, target) => {
        const sourceArray = source?.getArray();
        const targetArray = target?.getArray();
        if (!sourceArray || !targetArray || sourceArray.length !== targetArray.length) return false;
        for (let index = 0; index < sourceArray.length; index++) {
            if (sourceArray[index] !== targetArray[index]) return false;
        }
        return true;
    };
    for (const material of root.listMaterials()) {
        const infos = textureInfoGetters.map((name) => material[name]()).filter(Boolean);
        const remappedChannels = new Map();
        for (const info of infos) {
            const sourceChannel = info.getTexCoord() ?? 0;
            if (sourceChannel < 2) continue;
            const primitives = materialPrimitives.get(material) ?? [];
            let targetChannel = remappedChannels.get(sourceChannel);
            if (targetChannel === undefined) {
                targetChannel = [0, 1].find((candidate) => primitives.length > 0 &&
                    primitives.every((primitive) => coordinatesMatch(
                        primitive.getAttribute('TEXCOORD_' + sourceChannel),
                        primitive.getAttribute('TEXCOORD_' + candidate),
                    )));
            }
            if (targetChannel === undefined) {
                throw new Error(`Material ${material.getName()} uses unique unsupported UV channel ${sourceChannel}`);
            }
            info.setTexCoord(targetChannel);
            remappedChannels.set(sourceChannel, targetChannel);
            uvChannelsRemapped++;
        }
        for (const sourceChannel of remappedChannels.keys()) {
            for (const primitive of materialPrimitives.get(material) ?? []) {
                primitive.setAttribute('TEXCOORD_' + sourceChannel, null);
            }
        }
    }

    for (const extension of [...root.listExtensionsUsed()]) {
        if (extension.extensionName === 'EXT_texture_webp' ||
            extension.extensionName === 'KHR_draco_mesh_compression') {
            extension.dispose();
        }
    }

    // GLTFast's Unity importer dereferences a missing byte buffer for some
    // valid sparse accessors (notably JOINTS_0). Materialize every accessor
    // before writing so the resulting GLB uses ordinary buffer views.
    for (const accessor of root.listAccessors()) {
        accessor.setSparse(false);
    }

    const unsupported = root.listExtensionsRequired()
        .map((extension) => extension.extensionName)
        .filter((name) => name === 'EXT_texture_webp' || name === 'KHR_draco_mesh_compression');
    if (unsupported.length) {
        throw new Error('Unsupported compression extensions remain: ' + unsupported.join(', '));
    }
    for (const texture of root.listTextures()) {
        if (texture.getMimeType() === 'image/webp') {
            throw new Error('WebP texture remains: ' + texture.getName());
        }
    }
    for (const material of root.listMaterials()) {
        for (const name of textureInfoGetters) {
            const info = material[name]();
            if (info && (info.getTexCoord() ?? 0) > 1) {
                throw new Error(`Material ${material.getName()} still uses unsupported UV channel ${info.getTexCoord()}`);
            }
        }
    }

    const binary = await io.writeBinary(document);
    await writeFile(output, binary);
    return {
        texturesConvertedToPng: convertedTextures,
        uvChannelsRemapped,
        meshes: root.listMeshes().length,
        skins: root.listSkins().length,
        animations: root.listAnimations().length,
        bytes: binary.byteLength,
        extensionsBefore: extensionNamesBefore,
        accessorsDensified: root.listAccessors().length,
    };
}

if (batch) {
    const sourceDir = resolve(args[1]);
    const resourcesDir = resolve(args[2]);
    let totalBytes = 0;
    let totalTextures = 0;
    for (let id = 1; id <= 151; id++) {
        const input = join(sourceDir, id + '.glb');
        const output = join(resourcesDir, String(id), 'Pokemon.glb');
        const result = await convertOne(input, output);
        totalBytes += result.bytes;
        totalTextures += result.texturesConvertedToPng;
        console.log('[' + id + '/151] converted ' + result.animations + ' animations, '
            + result.texturesConvertedToPng + ' PNG textures, ' + result.bytes + ' bytes');
    }
    console.log('Batch complete: ' + totalBytes + ' bytes and ' + totalTextures + ' converted textures.');
} else {
    const result = await convertOne(args[0], args[1]);
    console.log(JSON.stringify({ input: resolve(args[0]), output: resolve(args[1]), ...result }));
}
