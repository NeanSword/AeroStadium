using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    // Audited from native BodySkin tip triangle fans and inverse bind matrices.
    // glTF local X is mirrored exactly once for glTFast Unity coordinates.
    internal static class NativeSmokeVentLayout
    {
        readonly struct Point
        {
            internal readonly string bone;
            internal readonly Vector3 tip, outward;
            internal Point(string bone, Vector3 tip, Vector3 outward)
            { this.bone=bone; this.tip=tip; this.outward=outward; }
        }
        static readonly Point[] Species109 =
        {
            new Point("LFeelerA", new Vector3(-0.05255564f, -0.00347449f, 0.00027082f), new Vector3(-0.99780863f, -0.06596587f, 0.00514180f)),
            new Point("RFeelerA", new Vector3(-0.05159238f, -0.00282399f, 0.00064844f), new Vector3(-0.99842669f, -0.05465044f, 0.01254876f)),
            new Point("FeelerG", new Vector3(-0.04461708f, 0.00026125f, -0.00129879f), new Vector3(-0.99955946f, 0.00585272f, -0.02909681f)),
            new Point("FeelerB", new Vector3(-0.04447245f, -0.00357035f, 0.00067698f), new Vector3(-0.99667813f, -0.08001571f, 0.01517198f)),
            new Point("LFeelerB", new Vector3(-0.03621136f, 0.00044569f, 0.00170176f), new Vector3(-0.99882206f, 0.01229344f, 0.04693993f)),
            new Point("RFeelerB", new Vector3(-0.03954426f, -0.00087864f, 0.00079728f), new Vector3(-0.99955021f, -0.02220918f, 0.02015273f)),
            new Point("FeelerA", new Vector3(-0.04590208f, -0.00105748f, 0.00004127f), new Vector3(-0.99973433f, -0.02303160f, 0.00089874f)),
            new Point("FeelerC", new Vector3(-0.04606255f, -0.00272572f, -0.00179739f), new Vector3(-0.99749732f, -0.05902623f, -0.03892299f)),
            new Point("FeelerD", new Vector3(-0.04683716f, 0.00120969f, -0.00027428f), new Vector3(-0.99964951f, 0.02581846f, -0.00585403f)),
            new Point("FeelerE", new Vector3(-0.05171065f, -0.00032761f, 0.00011043f), new Vector3(-0.99997765f, -0.00633536f, 0.00213558f)),
            new Point("FeelerF", new Vector3(-0.05627934f, -0.00016309f, 0.00004186f), new Vector3(-0.99999552f, -0.00289793f, 0.00074371f)),
            new Point("FeelerH", new Vector3(-0.04615715f, 0.00045702f, 0.00151886f), new Vector3(-0.99941009f, 0.00989563f, 0.03288692f)),
            new Point("LFeelerC", new Vector3(-0.03539169f, -0.00069652f, -0.00249315f), new Vector3(-0.99733580f, -0.01962796f, -0.07025695f)),
            new Point("RFeelerC", new Vector3(-0.04446499f, 0.00092363f, -0.00132170f), new Vector3(-0.99934313f, 0.02075848f, -0.02970496f)),
            new Point("LFeelerD", new Vector3(-0.04434615f, 0.00199919f, -0.00222298f), new Vector3(-0.99773514f, 0.04497946f, -0.05001431f)),
            new Point("RFeelerD", new Vector3(-0.04736509f, -0.00051180f, 0.00458373f), new Vector3(-0.99529242f, -0.01075451f, 0.09631896f)),
            new Point("LFeelerE", new Vector3(-0.04616765f, 0.00183337f, 0.00125132f), new Vector3(-0.99884620f, 0.03966540f, 0.02707259f)),
            new Point("RFeelerE", new Vector3(-0.04998470f, -0.00037480f, 0.00182318f), new Vector3(-0.99930740f, -0.00749311f, 0.03644955f)),
            new Point("LFeelerF", new Vector3(-0.04752516f, 0.00121544f, -0.00047882f), new Vector3(-0.99962243f, 0.02556496f, -0.01007135f)),
            new Point("RFeelerF", new Vector3(-0.04563404f, 0.00304611f, -0.00077028f), new Vector3(-0.99763810f, 0.06659312f, -0.01683974f)),
            new Point("LFeelerG", new Vector3(-0.05890166f, 0.00330933f, -0.00300195f), new Vector3(-0.99713530f, 0.05602310f, -0.05081941f)),
            new Point("RFeelerG", new Vector3(-0.06040578f, 0.00134420f, 0.00022799f), new Vector3(-0.99974538f, 0.02224719f, 0.00377327f)),
        };
        static readonly Point[] Species110 =
        {
            new Point("FeelerA", new Vector3(-0.04957958f, 0.00238860f, 0.00254496f), new Vector3(-0.99753123f, 0.04805809f, 0.05120417f)),
            new Point("FeelerG", new Vector3(-0.05022551f, -0.00018880f, -0.00087976f), new Vector3(-0.99983956f, -0.00375849f, -0.01751346f)),
            new Point("FeelerB", new Vector3(-0.04800034f, 0.00030409f, -0.00026642f), new Vector3(-0.99996453f, 0.00633487f, -0.00555011f)),
            new Point("FeelerC", new Vector3(-0.05576310f, 0.00000001f, 0.00000006f), new Vector3(-1.00000000f, 0.00000013f, 0.00000105f)),
            new Point("FeelerD", new Vector3(-0.04497498f, -0.00427165f, 0.00117771f), new Vector3(-0.99518176f, -0.09452073f, 0.02605967f)),
            new Point("FeelerE", new Vector3(-0.04937266f, -0.00431355f, -0.00127671f), new Vector3(-0.99587481f, -0.08700682f, -0.02575206f)),
            new Point("FeelerF", new Vector3(-0.05524727f, 0.00153819f, 0.00000002f), new Vector3(-0.99961264f, 0.02783111f, 0.00000036f)),
            new Point("FeelerH", new Vector3(-0.04943584f, 0.00000004f, -0.00000007f), new Vector3(-1.00000000f, 0.00000089f, -0.00000147f)),
            new Point("FeelerI", new Vector3(-0.06736125f, 0.00000000f, -0.00000000f), new Vector3(-1.00000000f, 0.00000007f, -0.00000007f)),
            new Point("FeelerJ", new Vector3(-0.05133817f, -0.00244970f, -0.00274098f), new Vector3(-0.99744608f, -0.04759506f, -0.05325440f)),
            new Point("FeelerK", new Vector3(-0.05039470f, -0.00044915f, -0.00124313f), new Vector3(-0.99965621f, -0.00890953f, -0.02465942f)),
            new Point("FeelerL", new Vector3(-0.04808053f, 0.01005362f, -0.00553248f), new Vector3(-0.97268023f, 0.20338713f, -0.11192337f)),
            new Point("FeelerM", new Vector3(-0.04893409f, 0.00400428f, -0.00195974f), new Vector3(-0.99587563f, 0.08149259f, -0.03988331f)),
            new Point("FeelerN", new Vector3(-0.05151795f, -0.00000001f, 0.00000003f), new Vector3(-1.00000000f, -0.00000022f, 0.00000063f)),
            new Point("FeelerO", new Vector3(-0.03425723f, 0.00158867f, -0.00131047f), new Vector3(-0.99819791f, 0.04629105f, -0.03818479f)),
            new Point("FeelerP", new Vector3(-0.02987580f, -0.00108352f, 0.00123485f), new Vector3(-0.99849156f, -0.03621263f, 0.04127051f)),
            new Point("FeelerQ", new Vector3(-0.03233311f, 0.00000913f, -0.00008692f), new Vector3(-0.99999635f, 0.00028250f, -0.00268836f)),
            new Point("FeelerR", new Vector3(-0.03495335f, 0.00064764f, 0.00144453f), new Vector3(-0.99897594f, 0.01850977f, 0.04128517f)),
            new Point("FeelerS", new Vector3(-0.03412569f, 0.00191106f, 0.00003315f), new Vector3(-0.99843518f, 0.05591290f, 0.00096991f)),
            new Point("FeelerT", new Vector3(-0.03548117f, 0.00086184f, 0.00067142f), new Vector3(-0.99952629f, 0.02427844f, 0.01891439f)),
            new Point("FeelerU", new Vector3(-0.03189198f, -0.00813542f, -0.00327442f), new Vector3(-0.96421034f, -0.24596318f, -0.09899770f)),
        };

        internal static NativePersistentSmoke.Vent[] Resolve(Transform root, int species)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            Point[] points = species == 109 ? Species109 : species == 110 ? Species110
                : throw new ArgumentOutOfRangeException(nameof(species));
            var nodes = new Dictionary<string, Transform>();
            var required = new HashSet<string>();
            foreach (Point point in points) required.Add(point.bone);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (!required.Contains(node.name)) continue;
                if (nodes.ContainsKey(node.name))
                    throw new InvalidOperationException("Duplicate native wart bone: " + node.name);
                nodes.Add(node.name, node);
            }
            var result = new NativePersistentSmoke.Vent[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Point point = points[i];
                if (!nodes.TryGetValue(point.bone, out Transform bone))
                    throw new InvalidOperationException("Missing native wart bone: " + point.bone);
                result[i] = new NativePersistentSmoke.Vent { head = bone,
                    headLocalPoint = point.tip, headLocalDirection = point.outward };
            }
            return result;
        }
    }
}
