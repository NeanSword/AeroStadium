using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Original, deterministic creature performance sampled independently of a
    /// rig. Angles are degrees. Root and foot translations are fractions of
    /// fitted model height in model space (X lateral, Y up, Z forward).
    /// RootOffset does not include the profile's standing hover height.
    /// FootAdvance is measured from the initial support position; a nonzero
    /// advance is held while a paw is planted, and removed during a return step.
    /// WingDrive and TailDrive describe total angular drive before chain sharing.
    /// </summary>
    public readonly struct CreaturePerformance
    {
        public readonly Vector3 RootOffset;
        public readonly Vector3 RootEuler;
        public readonly float BodyPitch, BodyRoll, HeadPitch, HeadYaw, JawOpen;
        public readonly float ArmSwingLeft, ArmSwingRight, WingDrive, TailDrive;
        public readonly float FootLiftLeft, FootLiftRight, FootAdvanceLeft, FootAdvanceRight;
        public readonly float EarDriveLeft, EarDriveRight;
        public readonly float Intensity;

        struct Pose
        {
            public Vector3 RootOffset, RootEuler;
            public float BodyPitch, BodyRoll, HeadPitch, HeadYaw, JawOpen;
            public float ArmSwingLeft, ArmSwingRight, WingDrive, TailDrive;
            public float FootLiftLeft, FootLiftRight, FootAdvanceLeft, FootAdvanceRight;
            public float EarDriveLeft, EarDriveRight, Intensity;
        }

        CreaturePerformance(Pose pose)
        {
            RootOffset = pose.RootOffset;
            RootEuler = pose.RootEuler;
            BodyPitch = pose.BodyPitch; BodyRoll = pose.BodyRoll;
            HeadPitch = pose.HeadPitch; HeadYaw = pose.HeadYaw; JawOpen = pose.JawOpen;
            ArmSwingLeft = pose.ArmSwingLeft; ArmSwingRight = pose.ArmSwingRight;
            WingDrive = Mathf.Clamp(pose.WingDrive, -82f, 82f); TailDrive = pose.TailDrive;
            FootLiftLeft = Mathf.Max(0f, pose.FootLiftLeft);
            FootLiftRight = Mathf.Max(0f, pose.FootLiftRight);
            FootAdvanceLeft = pose.FootAdvanceLeft; FootAdvanceRight = pose.FootAdvanceRight;
            EarDriveLeft = pose.EarDriveLeft; EarDriveRight = pose.EarDriveRight;
            Intensity = Mathf.Clamp01(pose.Intensity);
        }

        /// <summary>Blend complete performance frames, including support targets.</summary>
        public static CreaturePerformance Blend(CreaturePerformance from, CreaturePerformance to, float weight)
        {
            weight = Mathf.Clamp01(weight);
            return new CreaturePerformance(new Pose
            {
                RootOffset = Vector3.Lerp(from.RootOffset, to.RootOffset, weight),
                RootEuler = Vector3.Lerp(from.RootEuler, to.RootEuler, weight),
                BodyPitch = Mathf.Lerp(from.BodyPitch, to.BodyPitch, weight),
                BodyRoll = Mathf.Lerp(from.BodyRoll, to.BodyRoll, weight),
                HeadPitch = Mathf.Lerp(from.HeadPitch, to.HeadPitch, weight),
                HeadYaw = Mathf.Lerp(from.HeadYaw, to.HeadYaw, weight),
                JawOpen = Mathf.Lerp(from.JawOpen, to.JawOpen, weight),
                ArmSwingLeft = Mathf.Lerp(from.ArmSwingLeft, to.ArmSwingLeft, weight),
                ArmSwingRight = Mathf.Lerp(from.ArmSwingRight, to.ArmSwingRight, weight),
                WingDrive = Mathf.Lerp(from.WingDrive, to.WingDrive, weight),
                TailDrive = Mathf.Lerp(from.TailDrive, to.TailDrive, weight),
                FootLiftLeft = Mathf.Lerp(from.FootLiftLeft, to.FootLiftLeft, weight),
                FootLiftRight = Mathf.Lerp(from.FootLiftRight, to.FootLiftRight, weight),
                FootAdvanceLeft = Mathf.Lerp(from.FootAdvanceLeft, to.FootAdvanceLeft, weight),
                FootAdvanceRight = Mathf.Lerp(from.FootAdvanceRight, to.FootAdvanceRight, weight),
                EarDriveLeft = Mathf.Lerp(from.EarDriveLeft, to.EarDriveLeft, weight),
                EarDriveRight = Mathf.Lerp(from.EarDriveRight, to.EarDriveRight, weight),
                Intensity = Mathf.Lerp(from.Intensity, to.Intensity, weight)
            });
        }

        public static CreaturePerformance Sample(int species, PokemonAnatomy anatomy,
            PokemonMotionAction action, float t, float actionAge, PokemonMotionProfile profile)
        {
            t = Mathf.Max(0f, t); actionAge = Mathf.Max(0f, actionAge);
            var pose = new Pose();
            Rest(anatomy, t, profile, ref pose);
            if (action == PokemonMotionAction.Idle)
                Idle(species, anatomy, t, profile, ref pose);
            else if (action == PokemonMotionAction.Damage)
                Damage(anatomy, actionAge, profile, ref pose);
            else if (action == PokemonMotionAction.Faint)
                Faint(anatomy, actionAge, profile, ref pose);
            else
                Action(species, anatomy, action, actionAge, profile, ref pose);
            // Legless ground supports are compressed/articulated above their
            // base. Translating their full visual root down would bury them.
            if (profile.FloatHeight <= 0f && (anatomy == PokemonAnatomy.Blob
                || anatomy == PokemonAnatomy.Shell || anatomy == PokemonAnatomy.Serpent
                || anatomy == PokemonAnatomy.Fish))
                pose.RootOffset.y = Mathf.Max(0f, pose.RootOffset.y);
            else if (profile.FloatHeight > 0f)
                pose.RootOffset.y = Mathf.Max(-profile.FloatHeight * .85f, pose.RootOffset.y);
            return new CreaturePerformance(pose);
        }

        static void Rest(PokemonAnatomy anatomy, float t, PokemonMotionProfile profile, ref Pose pose)
        {
            float breath = Mathf.Sin(t * profile.BreathRate * Mathf.PI * 2f);
            pose.BodyPitch = breath * profile.BreathSize;
            pose.HeadPitch = -breath * profile.BreathSize * .35f;
            pose.Intensity = .12f;
            // Continuous locomotor drives remain joint motion; root movement is
            // limited to force response and support shifts, never a substitute.
            if (anatomy == PokemonAnatomy.Bird || anatomy == PokemonAnatomy.Bat
                || (anatomy == PokemonAnatomy.Insect && profile.FloatHeight > 0f))
            {
                if (profile.FloatHeight > 0f && profile.WingRate > 0f)
                {
                    float wing = FlightStroke(t, profile.WingRate);
                    float amplitude = Mathf.Clamp(profile.WingSize * 2.1f, 32f, 66f);
                    pose.WingDrive = wing * amplitude;
                    pose.BodyPitch += wing * 4.5f;
                    pose.RootOffset.y = -wing * .011f;
                }
            }
            if (anatomy == PokemonAnatomy.Fish || anatomy == PokemonAnatomy.Serpent)
                pose.TailDrive = Mathf.Sin(t * (anatomy == PokemonAnatomy.Fish ? 4.1f : 2.7f))
                    * Mathf.Max(10f, profile.TailSize * 1.6f);
            else if (profile.TailSize > 0f)
                pose.TailDrive = Mathf.Sin(t * 1.85f - .5f) * profile.TailSize * .55f;
        }

        static void Idle(int species, PokemonAnatomy anatomy, float t,
            PokemonMotionProfile profile, ref Pose pose)
        {
            float period = 5.35f + species % 5 * .23f + Mathf.Max(0f, profile.Weight - 1f) * .30f;
            float c = Mathf.Repeat(t + species % 3 * .17f, period);
            float attention = Beat(c, .30f, .68f, 1.03f, 1.45f);
            float prepare = Beat(c, 1.34f, 1.72f, 2.23f, 2.77f);
            float gesture = Beat(c, 2.48f, 2.90f, 3.42f, 4.48f);
            float settle = Beat(c, 3.96f, 4.20f, 4.47f, 4.95f);
            float direction = species % 2 == 0 ? -1f : 1f;
            pose.HeadYaw += direction * (attention * 22f - settle * 8f);
            pose.HeadPitch += prepare * 6f - gesture * 8f;
            pose.BodyPitch += prepare * 5f - gesture * 6f;
            pose.BodyRoll = direction * (attention * 2.5f - settle * 1.5f);
            pose.ArmSwingLeft = -gesture * 13f - prepare * 5f;
            pose.ArmSwingRight = -gesture * 19f - prepare * 4f;
            if (profile.FloatHeight <= 0f && profile.WingSize > 0f)
                pose.WingDrive = gesture * profile.WingSize * 4f - settle * profile.WingSize;
            pose.TailDrive += (gesture - settle * .6f) * profile.TailSize;
            pose.EarDriveLeft = profile.EarSize * 2.4f * Beat(c, .45f, .55f, .68f, .99f);
            pose.EarDriveRight = profile.EarSize * 2f * Beat(c, .63f, .74f, .90f, 1.20f);
            pose.Intensity = .18f + Mathf.Max(attention, Mathf.Max(prepare, gesture)) * .58f;

            switch (anatomy)
            {
                case PokemonAnatomy.Biped:
                case PokemonAnatomy.Quadruped:
                    Steps(c, profile.Weight, anatomy == PokemonAnatomy.Quadruped ? 1.15f : .85f, ref pose);
                    break;
                case PokemonAnatomy.Bird:
                    if (profile.FloatHeight <= 0f) Steps(c, profile.Weight, .72f, ref pose);
                    else AirBank(c, attention, gesture, direction, ref pose);
                    break;
                case PokemonAnatomy.Bat:
                    AirBank(c, attention, gesture, direction, ref pose);
                    break;
                case PokemonAnatomy.Insect:
                    if (profile.FloatHeight > 0f) AirBank(c, attention, gesture, direction, ref pose);
                    else Steps(c, profile.Weight, .55f, ref pose);
                    break;
                case PokemonAnatomy.Float:
                    pose.RootEuler.z = direction * (attention * 6f - gesture * 5f);
                    pose.RootOffset.x = direction * (attention * .018f - settle * .012f);
                    pose.RootOffset.y += gesture * .012f;
                    pose.BodyRoll += gesture * 5f;
                    break;
                case PokemonAnatomy.Serpent:
                    pose.BodyRoll += Mathf.Sin(t * 2.7f - .7f) * 4f;
                    pose.HeadPitch -= gesture * 11f;
                    pose.JawOpen = gesture * 13f;
                    break;
                case PokemonAnatomy.Fish:
                    pose.BodyRoll += attention * direction * 5f;
                    pose.HeadPitch -= gesture * 5f;
                    pose.JawOpen = gesture * 9f;
                    break;
                case PokemonAnatomy.Plant:
                    pose.BodyRoll += direction * gesture * (profile.Weight < 1f ? 8f : 4f);
                    pose.RootOffset.y = -prepare * .018f;
                    pose.JawOpen = gesture * 10f;
                    break;
                case PokemonAnatomy.Blob:
                    pose.RootOffset.y = -prepare * .022f;
                    pose.BodyPitch += gesture * 5f;
                    pose.BodyRoll += direction * attention * 4f;
                    pose.ArmSwingLeft = -gesture * 34f;
                    pose.ArmSwingRight = -gesture * 47f;
                    break;
                case PokemonAnatomy.Shell:
                    pose.BodyPitch *= .35f;
                    pose.BodyRoll *= .35f;
                    pose.HeadYaw *= .35f;
                    pose.HeadPitch *= .35f;
                    pose.ArmSwingLeft = pose.ArmSwingRight = 0f;
                    if (species == 90 || species == 91) pose.JawOpen = gesture * 19f;
                    break;
            }

            // Curated complete performances use the same support clock as the
            // family but change the intent, posing and secondary reactions.
            switch (species)
            {
                case 25: Pikachu(c, attention, prepare, gesture, ref pose); break;
                case 6: Charizard(c, attention, prepare, gesture, ref pose); break;
                case 9: Blastoise(c, attention, prepare, gesture, ref pose); break;
                case 133: Eevee(c, attention, prepare, gesture, ref pose); break;
                case 18: Pidgeot(t, c, attention, gesture, profile, ref pose); break;
                case 130: Gyarados(t, attention, prepare, gesture, ref pose); break;
                case 94: Gengar(c, attention, prepare, gesture, ref pose); break;
                case 65: Alakazam(c, attention, prepare, gesture, ref pose); break;
            }
        }

        static void Steps(float c, float weight, float scale, ref Pose pose)
        {
            float inertia = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(.60f, weight)), .62f, 1.28f) * scale;
            float left = Arch(c, 1.18f, 1.85f) + Arch(c, 3.35f, 4.02f);
            float right = Arch(c, 1.93f, 2.60f) + Arch(c, 4.12f, 4.76f);
            pose.FootLiftLeft = left * .065f * inertia;
            pose.FootLiftRight = right * .065f * inertia;
            pose.FootAdvanceLeft = Beat(c, 1.18f, 1.85f, 3.35f, 4.02f) * .085f * inertia;
            pose.FootAdvanceRight = Beat(c, 1.93f, 2.60f, 4.12f, 4.76f) * .085f * inertia;
            // The support side carries the centre of mass during each swing.
            pose.RootOffset.x += (right - left) * .020f * inertia;
            pose.RootOffset.y -= Mathf.Max(left, right) * .027f * inertia;
            pose.RootOffset.z += (pose.FootAdvanceLeft + pose.FootAdvanceRight) * .32f;
            pose.BodyRoll += (left - right) * 4.2f * inertia;
        }

        static void AirBank(float c, float attention, float gesture, float direction, ref Pose pose)
        {
            float glide = Beat(c, 3.72f, 3.96f, 4.34f, 4.82f);
            pose.RootEuler.z += direction * (attention * 9f - gesture * 7f);
            pose.RootEuler.y += direction * (attention * 4f - gesture * 3f);
            pose.BodyPitch -= gesture * 5f;
            pose.WingDrive = Mathf.Lerp(pose.WingDrive, 33f, glide * .72f);
            pose.Intensity = Mathf.Max(pose.Intensity, .60f);
        }

        static void Pikachu(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float crouch = Beat(c, 2.47f, 2.66f, 2.78f, 2.94f);
            float hop = Arch(c, 2.80f, 3.37f);
            float land = Beat(c, 3.25f, 3.40f, 3.48f, 3.73f);
            pose.HeadYaw = 32f * attention - 12f * Beat(c, 3.91f, 4.19f, 4.30f, 4.77f);
            pose.HeadPitch += prepare * 12f - hop * 15f;
            pose.BodyPitch += crouch * 15f - hop * 9f + land * 9f;
            pose.RootOffset.y += -crouch * .040f + hop * .075f - land * .034f;
            pose.FootLiftLeft = Mathf.Max(pose.FootLiftLeft, hop * .080f);
            pose.FootLiftRight = Mathf.Max(pose.FootLiftRight, hop * .080f);
            pose.ArmSwingLeft = -18f * prepare - 47f * hop + 12f * land;
            pose.ArmSwingRight = -28f * attention - 43f * hop + 12f * land;
            pose.JawOpen = 14f * Beat(c, 2.93f, 3.06f, 3.25f, 3.56f);
            pose.TailDrive += 20f * gesture - 13f * land;
            pose.EarDriveLeft += 17f * attention - 15f * crouch + 12f * hop;
            pose.EarDriveRight += 11f * attention - 15f * crouch + 17f * hop;
            pose.Intensity = Mathf.Max(pose.Intensity, hop * .98f);
        }

        static void Charizard(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float call = Beat(c, 2.64f, 2.95f, 3.39f, 4.19f);
            float wingSettle = Beat(c, 3.83f, 4.04f, 4.27f, 4.70f);
            pose.HeadYaw = attention * 25f;
            pose.HeadPitch += prepare * 8f - call * 27f;
            pose.BodyPitch += prepare * 9f - call * 13f;
            pose.JawOpen = call * 32f;
            pose.WingDrive = gesture * 46f - wingSettle * 8f;
            pose.ArmSwingLeft = -prepare * 9f - call * 33f;
            pose.ArmSwingRight = -prepare * 11f - call * 37f;
            pose.RootOffset.y -= prepare * .021f;
            pose.TailDrive += 17f * Beat(c, 2.82f, 3.19f, 3.71f, 4.60f);
            pose.Intensity = Mathf.Max(pose.Intensity, call * .92f);
        }

        static void Blastoise(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float brace = Beat(c, 2.39f, 2.86f, 3.52f, 4.43f);
            pose.HeadYaw = attention * 22f;
            pose.HeadPitch = prepare * 6f - brace * 13f;
            pose.BodyPitch = prepare * 12f + brace * 8f;
            pose.BodyRoll *= .65f;
            pose.RootOffset.y -= brace * .044f;
            pose.ArmSwingLeft = -brace * 35f - prepare * 8f;
            pose.ArmSwingRight = -brace * 31f - prepare * 13f;
            pose.JawOpen = 18f * Beat(c, 2.95f, 3.15f, 3.41f, 3.87f);
            pose.FootLiftLeft *= .70f; pose.FootLiftRight *= .70f;
            pose.Intensity = Mathf.Max(pose.Intensity, brace * .88f);
        }

        static void Eevee(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float sniff = Beat(c, 1.35f, 1.75f, 2.07f, 2.52f);
            float bark = Beat(c, 2.67f, 2.83f, 3.00f, 3.39f);
            float tailWag = Beat(c, 2.49f, 2.75f, 3.80f, 4.56f);
            pose.HeadYaw = 34f * attention - 17f * bark;
            pose.HeadPitch += sniff * 24f - bark * 18f;
            pose.BodyPitch += sniff * 10f - bark * 8f;
            pose.JawOpen = bark * 24f;
            pose.RootOffset.x += attention * .021f;
            pose.RootOffset.y -= sniff * .015f;
            pose.TailDrive += Mathf.Sin((c - 2.49f) * 11f) * 24f * tailWag;
            pose.EarDriveLeft += attention * 22f - sniff * 9f + bark * 11f;
            pose.EarDriveRight += attention * 15f - sniff * 6f + bark * 15f;
            pose.ArmSwingLeft = pose.ArmSwingRight = 0f;
            pose.Intensity = Mathf.Max(pose.Intensity, bark * .93f);
        }

        static void Pidgeot(float t, float c, float attention, float gesture,
            PokemonMotionProfile profile, ref Pose pose)
        {
            float wing = FlightStroke(t, profile.WingRate);
            float glide = Beat(c, 3.61f, 3.88f, 4.38f, 4.88f);
            pose.WingDrive = Mathf.Lerp(wing * 64f, 36f, glide * .88f);
            pose.RootEuler.z = attention * 13f - gesture * 10f;
            pose.RootEuler.y = attention * 6f - gesture * 5f;
            pose.RootOffset.y = -wing * .013f * (1f - glide * .6f);
            pose.BodyPitch += wing * 6f - gesture * 8f;
            pose.HeadPitch += -gesture * 15f;
            pose.HeadYaw = 27f * attention - 18f * gesture;
            pose.TailDrive += wing * 9f;
            pose.Intensity = .74f + Mathf.Abs(wing) * .18f;
        }

        static void Gyarados(float t, float attention, float prepare, float gesture, ref Pose pose)
        {
            pose.TailDrive = Mathf.Sin(t * 2.65f) * 31f + gesture * 12f;
            pose.BodyRoll = Mathf.Sin(t * 2.65f - .72f) * 7f;
            pose.RootEuler.z = Mathf.Sin(t * 2.65f - 1.2f) * 3.5f;
            pose.HeadYaw = attention * 24f;
            pose.HeadPitch += prepare * 10f - gesture * 29f;
            pose.BodyPitch += prepare * 8f - gesture * 12f;
            pose.JawOpen = gesture * 38f;
            pose.RootOffset.y += -prepare * .015f;
            pose.Intensity = Mathf.Max(pose.Intensity, gesture * .97f);
        }

        static void Gengar(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float tease = Beat(c, 2.36f, 2.82f, 3.57f, 4.39f);
            float laugh = Arch(c, 2.85f, 3.21f) + Arch(c, 3.24f, 3.60f);
            pose.HeadYaw = attention * 30f - tease * 12f;
            pose.HeadPitch += prepare * 8f - laugh * 8f;
            pose.BodyPitch += tease * 20f;
            pose.BodyRoll += tease * 5f;
            pose.ArmSwingLeft = -tease * 61f;
            pose.ArmSwingRight = -tease * 35f - laugh * 16f;
            pose.JawOpen = laugh * 20f;
            pose.RootOffset.y -= prepare * .022f;
            pose.EarDriveLeft = prepare * 11f;
            pose.EarDriveRight = tease * 9f;
            pose.Intensity = Mathf.Max(pose.Intensity, tease * .94f);
        }

        static void Alakazam(float c, float attention, float prepare, float gesture, ref Pose pose)
        {
            float focus = Beat(c, 2.34f, 2.93f, 3.62f, 4.63f);
            pose.HeadYaw = attention * 22f * (1f - focus);
            pose.HeadPitch += -focus * 17f;
            pose.BodyPitch += prepare * 5f - focus * 7f;
            pose.ArmSwingLeft = -focus * 68f;
            pose.ArmSwingRight = -focus * 59f;
            pose.RootOffset.y -= prepare * .018f;
            pose.FootLiftLeft *= .56f; pose.FootLiftRight *= .56f;
            pose.EarDriveLeft = pose.EarDriveRight = -focus * 7f;
            pose.Intensity = Mathf.Max(pose.Intensity, focus * .88f);
        }

        static void Action(int species, PokemonAnatomy anatomy, PokemonMotionAction action,
            float age, PokemonMotionProfile profile, ref Pose pose)
        {
            bool show = action == PokemonMotionAction.Showcase;
            bool special = action == PokemonMotionAction.Special || show;
            float duration = show ? 1.6f : profile.AttackDuration;
            float u = Mathf.Clamp01(age / Mathf.Max(.01f, duration));
            float charge = Beat(u, 0f, .13f, .20f, .37f);
            float strike = Beat(u, .22f, .38f, .53f, .90f);
            float recoil = Beat(u, .51f, .60f, .66f, .91f);
            float follow = Beat(u, .40f, .59f, .70f, 1f);
            float inertia = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(.6f, profile.Weight)), .65f, 1.2f);
            pose.Intensity = Mathf.Clamp01(charge * .6f + strike);
            pose.BodyPitch += charge * 16f - strike * (special ? 16f : -23f) + recoil * 6f;
            pose.HeadPitch += charge * 12f - strike * (special ? 20f : -8f);
            pose.HeadYaw = 0f;
            pose.JawOpen = strike * (special ? 27f : 14f);
            pose.ArmSwingLeft = charge * 12f - strike * (special ? 58f : 32f);
            pose.ArmSwingRight = charge * 17f - strike * (special ? 58f : 73f);
            pose.TailDrive += -charge * 12f + follow * 19f;
            pose.WingDrive += -charge * 12f + strike * 42f;
            pose.EarDriveLeft = pose.EarDriveRight = -charge * 13f + follow * 11f;
            if (profile.FloatHeight <= 0f)
            {
                pose.RootOffset.y -= charge * .042f * inertia + recoil * .018f;
                if (!special && (anatomy == PokemonAnatomy.Biped || anatomy == PokemonAnatomy.Quadruped))
                {
                    pose.FootLiftRight = Arch(u, .12f, .48f) * .076f * inertia;
                    pose.FootAdvanceRight = Beat(u, .12f, .48f, .64f, 1f) * .105f * inertia;
                    pose.FootLiftRight += Arch(u, .64f, 1f) * .055f * inertia;
                    pose.RootOffset.x = -strike * .018f;
                    pose.RootOffset.z = pose.FootAdvanceRight * .52f;
                    pose.BodyRoll = -strike * 6f;
                }
            }
            else
            {
                pose.RootEuler.x += charge * -7f + strike * (special ? -4f : 14f);
                pose.RootOffset.z += strike * (special ? .024f : .09f);
            }

            switch (species)
            {
                case 6:
                    pose.WingDrive += strike * (special ? 29f : 9f);
                    pose.HeadPitch -= special ? strike * 10f : 0f;
                    pose.JawOpen = strike * (special ? 37f : 24f);
                    pose.TailDrive += follow * 12f;
                    break;
                case 9:
                    pose.BodyPitch = charge * 13f + strike * 8f - recoil * 17f;
                    pose.HeadPitch = -strike * 13f;
                    pose.RootOffset.z -= recoil * .031f;
                    pose.RootOffset.y -= strike * .035f;
                    pose.ArmSwingLeft = pose.ArmSwingRight = -strike * 43f + recoil * 16f;
                    break;
                case 25:
                    pose.EarDriveLeft += follow * 17f;
                    pose.EarDriveRight += follow * 12f;
                    pose.TailDrive += strike * 23f;
                    break;
                case 133:
                    pose.HeadPitch = charge * 16f - strike * 15f;
                    pose.ArmSwingLeft = pose.ArmSwingRight = 0f;
                    pose.JawOpen = strike * 27f;
                    if (!special)
                    {
                        float spring = Arch(u, .23f, .71f);
                        pose.RootOffset.y += spring * .067f;
                        pose.FootLiftLeft = Mathf.Max(pose.FootLiftLeft, spring * .078f);
                        pose.FootLiftRight = Mathf.Max(pose.FootLiftRight, spring * .078f);
                        pose.RootOffset.z += strike * .045f;
                        pose.BodyPitch += strike * 7f;
                    }
                    break;
                case 18:
                    if (!special)
                    {
                        pose.WingDrive = Mathf.Lerp(pose.WingDrive, -26f, strike);
                        pose.RootEuler.x += strike * 12f;
                        pose.RootOffset.y -= strike * .024f;
                    }
                    break;
                case 130:
                    pose.HeadPitch = charge * 19f - strike * (special ? 31f : -17f);
                    pose.JawOpen = strike * 42f;
                    pose.BodyPitch += charge * 10f + strike * 8f;
                    pose.TailDrive += follow * 17f;
                    break;
                case 94:
                    pose.BodyPitch += strike * 13f;
                    pose.ArmSwingLeft = -strike * (special ? 66f : 44f);
                    pose.ArmSwingRight = -strike * (special ? 84f : 80f);
                    pose.JawOpen = strike * 26f;
                    break;
                case 65:
                    pose.HeadPitch = -strike * 11f;
                    pose.ArmSwingLeft = -strike * (special ? 81f : 46f);
                    pose.ArmSwingRight = -strike * (special ? 76f : 67f);
                    pose.JawOpen = strike * 5f;
                    pose.BodyRoll = 0f;
                    break;
            }
            if (anatomy == PokemonAnatomy.Shell)
            {
                pose.BodyPitch *= .30f;
                pose.RootEuler *= .45f;
                pose.ArmSwingLeft = pose.ArmSwingRight = 0f;
            }
        }

        static void Damage(PokemonAnatomy anatomy, float age, PokemonMotionProfile profile, ref Pose pose)
        {
            float u = Mathf.Clamp01(age / Mathf.Max(.01f, profile.HitDuration));
            float impact = Beat(u, 0f, .10f, .25f, .77f);
            float recover = Beat(u, .38f, .54f, .62f, 1f);
            pose.Intensity = impact;
            pose.RootOffset.z = -impact * .045f;
            pose.RootOffset.y -= recover * .026f;
            pose.BodyPitch = -impact * 20f + recover * 8f;
            pose.HeadPitch = impact * 17f - recover * 7f;
            pose.BodyRoll = impact * 7f;
            pose.ArmSwingLeft = impact * 22f - recover * 10f;
            pose.ArmSwingRight = impact * 28f - recover * 12f;
            pose.JawOpen = impact * 20f;
            pose.TailDrive += impact * 18f;
            pose.EarDriveLeft = pose.EarDriveRight = -impact * 18f;
            if (profile.FloatHeight > 0f)
            {
                pose.RootEuler.z = impact * 11f - recover * 4f;
                pose.WingDrive *= 1f - impact * .45f;
            }
            if (anatomy == PokemonAnatomy.Shell) pose.BodyPitch *= .30f;
        }

        static void Faint(PokemonAnatomy anatomy, float age, PokemonMotionProfile profile, ref Pose pose)
        {
            float u = Mathf.Clamp01(age / Mathf.Max(.01f, profile.FaintDuration));
            float loseStrength = Ease(Mathf.Clamp01(u / .42f));
            float collapse = Ease(Mathf.Clamp01((u - .25f) / .75f));
            pose.Intensity = 1f - collapse;
            pose.BodyPitch = loseStrength * 12f + collapse * 23f;
            pose.BodyRoll = collapse * 15f;
            pose.HeadPitch = loseStrength * 14f + collapse * 20f;
            pose.HeadYaw = collapse * -8f;
            pose.JawOpen = loseStrength * 9f * (1f - collapse);
            pose.ArmSwingLeft = collapse * 27f;
            pose.ArmSwingRight = collapse * 23f;
            pose.TailDrive *= 1f - collapse;
            pose.WingDrive = Mathf.Lerp(pose.WingDrive, -23f, collapse);
            pose.EarDriveLeft = pose.EarDriveRight = -collapse * 22f;
            pose.RootOffset.y = profile.FloatHeight > 0f
                ? -profile.FloatHeight * collapse * .78f : -collapse * .051f;
            pose.RootOffset.x = collapse * .024f;
            pose.RootEuler.z = collapse * (profile.FloatHeight > 0f ? 13f : 3f);
            // Grounded supports stay fixed while the knees and upper body fold.
            pose.FootAdvanceLeft = pose.FootAdvanceRight = 0f;
            if (anatomy == PokemonAnatomy.Shell)
            {
                pose.BodyPitch *= .25f;
                pose.BodyRoll *= .35f;
            }
        }

        // Smooth asymmetric wing stroke: a strong short downstroke and a long
        // recovery. Both reversals have zero velocity instead of a rigid sawtooth.
        static float FlightStroke(float t, float rate)
        {
            float p = Mathf.Repeat(t * Mathf.Max(.1f, rate), 1f);
            return p < .36f ? Mathf.Lerp(1f, -1f, Ease(p / .36f))
                : Mathf.Lerp(-1f, 1f, Ease((p - .36f) / .64f));
        }

        static float Beat(float t, float start, float peak, float holdEnd, float end)
        {
            if (t <= start || t >= end) return 0f;
            if (t < peak) return Ease((t - start) / Mathf.Max(.0001f, peak - start));
            if (t <= holdEnd) return 1f;
            return 1f - Ease((t - holdEnd) / Mathf.Max(.0001f, end - holdEnd));
        }

        static float Arch(float t, float start, float end)
        {
            if (t <= start || t >= end) return 0f;
            float u = Ease((t - start) / Mathf.Max(.0001f, end - start));
            return 4f * u * (1f - u);
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
