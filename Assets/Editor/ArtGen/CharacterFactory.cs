using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Chibi (buyuk kafa / kucuk govde) low-poly karakter ureteci.
    ///
    /// Uretilen sey sadece bir mesh degil; tam bir oynatilabilir karakter:
    ///   - Humanoid isimlendirmeli kemik hiyerarsisi (T-pose)
    ///   - Rigid skinning (her govde parcasi tek kemige bagli - chibi karakterde
    ///     uzuvlar zaten burulmaz, bu yuzden agirlik boyamaya gerek yok)
    ///   - AvatarBuilder ile uretilmis Humanoid Avatar
    ///
    /// Avatar sayesinde projedeki mevcut Mixamo animasyonlari (Assets/Karakter/*.fbx)
    /// bu karaktere retarget edilir. TEK SART: o FBX'lerin Rig sekmesinde
    /// Animation Type = Humanoid olmasi. Generic ise retarget calismaz.
    /// </summary>
    public static class CharacterFactory
    {
        // --- Olculer (metre). Toplam boy ~1.80, kafa boyun %30'u: chibi orani. ---
        private const float TotalHeight = 1.80f;
        private const float HipsY = 0.70f;
        private const float SpineY = 0.86f;
        private const float ChestY = 1.02f;
        private const float NeckY = 1.20f;
        private const float HeadY = 1.30f;
        private const float ShoulderY = 1.11f;
        private const float ArmSpan = 0.21f;
        private const float LegSpan = 0.105f;
        private const float KneeY = 0.37f;
        private const float AnkleY = 0.08f;

        private const float HeadRadius = 0.245f;
        private const float TorsoWidth = 0.40f;
        private const float TorsoDepth = 0.26f;
        private const float LimbThickness = 0.115f;

        /// <summary>Kemik dizisindeki sabit sira. Mesh vertexleri bu indekslere baglanir.</summary>
        private enum Bone
        {
            Hips = 0,
            Spine,
            Chest,
            Neck,
            Head,
            LeftShoulder,
            LeftUpperArm,
            LeftLowerArm,
            LeftHand,
            RightShoulder,
            RightUpperArm,
            RightLowerArm,
            RightHand,
            LeftUpperLeg,
            LeftLowerLeg,
            LeftFoot,
            LeftToes,
            RightUpperLeg,
            RightLowerLeg,
            RightFoot,
            RightToes,
            Count
        }

        public enum HairStyle { Short, Buzz, Ponytail, None }
        public enum HeadGear { None, Cap, Helmet, Beanie }
        public enum Outfit { TShirt, TacticalVest, Hoodie }

        public struct CharacterStyle
        {
            public string Name;
            public Color Skin;
            public Color Hair;
            public Color Shirt;
            public Color ShirtAccent;
            public Color Pants;
            public Color Shoes;
            public Color GearColor;
            public HairStyle Hair_;
            public HeadGear Gear;
            public Outfit Outfit_;
            public bool HasBeard;
        }

        /// <summary>Projeye eklenecek iki baslangic karakteri.</summary>
        public static CharacterStyle[] DefaultRoster()
        {
            return new[]
            {
                new CharacterStyle
                {
                    Name = "Ranger",
                    Skin = ToonPalette.SkinLight,
                    Hair = ToonPalette.HairBrown,
                    Shirt = ToonPalette.ShirtWhite,
                    ShirtAccent = ToonPalette.ShirtBlue,
                    Pants = ToonPalette.PantsDark,
                    Shoes = ToonPalette.ShoeDark,
                    GearColor = ToonPalette.CapRed,
                    Hair_ = HairStyle.Short,
                    Gear = HeadGear.None,
                    Outfit_ = Outfit.TShirt,
                    HasBeard = true
                },
                new CharacterStyle
                {
                    Name = "Commando",
                    Skin = ToonPalette.SkinMid,
                    Hair = ToonPalette.HairBlack,
                    Shirt = ToonPalette.VestGreen,
                    ShirtAccent = ToonPalette.PantsKhaki,
                    Pants = ToonPalette.PantsKhaki,
                    Shoes = ToonPalette.ShoeDark,
                    GearColor = ToonPalette.ShirtBlue,
                    Hair_ = HairStyle.Buzz,
                    Gear = HeadGear.Helmet,
                    Outfit_ = Outfit.TacticalVest,
                    HasBeard = false
                }
            };
        }

        public static GameObject Generate(CharacterStyle style, Material toonMaterial)
        {
            GameObject root = new($"Character_{style.Name}");

            Transform[] bones = BuildSkeleton(root.transform);
            Mesh mesh = BuildMesh(style, bones, root.transform);
            mesh = ArtGenIO.SaveMesh(mesh, $"Character_{style.Name}", "Characters");

            GameObject meshHolder = new("Mesh");
            meshHolder.transform.SetParent(root.transform, false);

            SkinnedMeshRenderer renderer = meshHolder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = toonMaterial;
            renderer.bones = bones;
            renderer.rootBone = bones[(int)Bone.Hips];
            renderer.updateWhenOffscreen = false;
            renderer.quality = SkinQuality.Bone1; // Rigid skinning: tek kemik yeter, mobilde ucuz.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.localBounds = new Bounds(new Vector3(0f, TotalHeight * 0.5f, 0f),
                new Vector3(1.2f, TotalHeight + 0.4f, 1.2f));

            Avatar avatar = SaveAvatar(BuildAvatar(root, bones), style.Name);
            Animator animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            AddSockets(bones);

            return root;
        }

        #region Iskelet

        private static Transform[] BuildSkeleton(Transform root)
        {
            Transform[] bones = new Transform[(int)Bone.Count];

            // Not: Unity'de karakter +Z'ye bakar ve kendi sagi +X'tir.
            // Yani SOL uzuvlar negatif X'te durur.
            bones[(int)Bone.Hips] = NewBone(root, "Hips", new Vector3(0f, HipsY, 0f));
            bones[(int)Bone.Spine] = NewBone(bones[(int)Bone.Hips], "Spine", new Vector3(0f, SpineY, 0f));
            bones[(int)Bone.Chest] = NewBone(bones[(int)Bone.Spine], "Chest", new Vector3(0f, ChestY, 0f));
            bones[(int)Bone.Neck] = NewBone(bones[(int)Bone.Chest], "Neck", new Vector3(0f, NeckY, 0f));
            bones[(int)Bone.Head] = NewBone(bones[(int)Bone.Neck], "Head", new Vector3(0f, HeadY, 0f));

            BuildArm(bones, Bone.LeftShoulder, "Left", -1f);
            BuildArm(bones, Bone.RightShoulder, "Right", 1f);
            BuildLeg(bones, Bone.LeftUpperLeg, "Left", -1f);
            BuildLeg(bones, Bone.RightUpperLeg, "Right", 1f);

            return bones;
        }

        private static void BuildArm(Transform[] bones, Bone firstBone, string side, float sign)
        {
            int shoulderIndex = (int)firstBone;
            Transform chest = bones[(int)Bone.Chest];

            bones[shoulderIndex] = NewBone(chest, side + "Shoulder",
                new Vector3(sign * 0.075f, ShoulderY, 0f));
            bones[shoulderIndex + 1] = NewBone(bones[shoulderIndex], side + "UpperArm",
                new Vector3(sign * ArmSpan, ShoulderY, 0f));
            bones[shoulderIndex + 2] = NewBone(bones[shoulderIndex + 1], side + "LowerArm",
                new Vector3(sign * (ArmSpan + 0.22f), ShoulderY, 0f));
            bones[shoulderIndex + 3] = NewBone(bones[shoulderIndex + 2], side + "Hand",
                new Vector3(sign * (ArmSpan + 0.42f), ShoulderY, 0f));
        }

        private static void BuildLeg(Transform[] bones, Bone firstBone, string side, float sign)
        {
            int upperIndex = (int)firstBone;
            Transform hips = bones[(int)Bone.Hips];

            bones[upperIndex] = NewBone(hips, side + "UpperLeg",
                new Vector3(sign * LegSpan, HipsY, 0f));
            bones[upperIndex + 1] = NewBone(bones[upperIndex], side + "LowerLeg",
                new Vector3(sign * LegSpan, KneeY, 0f));
            bones[upperIndex + 2] = NewBone(bones[upperIndex + 1], side + "Foot",
                new Vector3(sign * LegSpan, AnkleY, 0f));
            bones[upperIndex + 3] = NewBone(bones[upperIndex + 2], side + "Toes",
                new Vector3(sign * LegSpan, 0.03f, 0.12f));
        }

        private static Transform NewBone(Transform parent, string name, Vector3 worldPosition)
        {
            GameObject bone = new(name);
            bone.transform.SetParent(parent, false);
            bone.transform.position = worldPosition;
            bone.transform.rotation = Quaternion.identity;
            return bone.transform;
        }

        /// <summary>Silah ve efektlerin baglanacagi sabit noktalar.</summary>
        private static void AddSockets(Transform[] bones)
        {
            GameObject weaponSocket = new("WeaponSocket");
            weaponSocket.transform.SetParent(bones[(int)Bone.RightHand], false);
            weaponSocket.transform.localPosition = new Vector3(0.02f, 0f, 0.06f);
            weaponSocket.transform.localRotation = Quaternion.identity;

            GameObject muzzle = new("MuzzlePoint");
            muzzle.transform.SetParent(weaponSocket.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);

            GameObject headTop = new("HeadAnchor");
            headTop.transform.SetParent(bones[(int)Bone.Head], false);
            headTop.transform.localPosition = new Vector3(0f, HeadRadius + 0.22f, 0f);
        }

        #endregion

        #region Mesh

        private static Mesh BuildMesh(CharacterStyle style, Transform[] bones, Transform root)
        {
            LowPolyMeshBuilder builder = new();

            BuildLegs(builder, style);
            BuildTorso(builder, style);
            BuildArms(builder, style);
            BuildHead(builder, style);

            Mesh mesh = builder.Build($"Character_{style.Name}", false);

            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;

            mesh.bindposes = bindposes;
            return mesh;
        }

        private static void BuildLegs(LowPolyMeshBuilder builder, CharacterStyle style)
        {
            BuildLeg(builder, style, -1f, Bone.LeftUpperLeg);
            BuildLeg(builder, style, 1f, Bone.RightUpperLeg);
        }

        private static void BuildLeg(LowPolyMeshBuilder builder, CharacterStyle style, float sign, Bone firstBone)
        {
            float x = sign * LegSpan;
            int upper = (int)firstBone;

            builder.SetBone(upper);
            builder.AddTaperedBox(
                new Vector3(x, (HipsY + KneeY) * 0.5f, 0f),
                new Vector3(LimbThickness + 0.02f, HipsY - KneeY, LimbThickness + 0.02f),
                new Vector2(LimbThickness + 0.02f, LimbThickness + 0.02f),
                style.Pants);

            builder.SetBone(upper + 1);
            builder.AddTaperedBox(
                new Vector3(x, (KneeY + AnkleY) * 0.5f, 0f),
                new Vector3(LimbThickness, KneeY - AnkleY, LimbThickness),
                new Vector2(LimbThickness, LimbThickness),
                style.Outfit_ == Outfit.TShirt ? style.Skin : style.Pants);

            // Ayakkabi: one dogru uzayan basik kutu.
            builder.SetBone(upper + 2);
            builder.AddTaperedBox(
                new Vector3(x, AnkleY * 0.55f, 0.045f),
                new Vector3(LimbThickness + 0.025f, AnkleY + 0.03f, LimbThickness + 0.11f),
                new Vector2(LimbThickness + 0.015f, LimbThickness + 0.09f),
                style.Shoes);
        }

        private static void BuildTorso(LowPolyMeshBuilder builder, CharacterStyle style)
        {
            builder.SetBone((int)Bone.Hips);
            builder.AddTaperedBox(
                new Vector3(0f, HipsY + 0.06f, 0f),
                new Vector3(TorsoWidth * 0.92f, 0.16f, TorsoDepth),
                new Vector2(TorsoWidth * 0.95f, TorsoDepth),
                style.Pants);

            builder.SetBone((int)Bone.Spine);
            builder.AddTaperedBox(
                new Vector3(0f, SpineY + 0.03f, 0f),
                new Vector3(TorsoWidth * 0.95f, 0.20f, TorsoDepth),
                new Vector2(TorsoWidth, TorsoDepth + 0.01f),
                style.Shirt);

            builder.SetBone((int)Bone.Chest);
            builder.AddTaperedBox(
                new Vector3(0f, ChestY + 0.06f, 0f),
                new Vector3(TorsoWidth, 0.22f, TorsoDepth + 0.01f),
                new Vector2(TorsoWidth * 0.88f, TorsoDepth * 0.95f),
                style.Shirt);

            if (style.Outfit_ == Outfit.TacticalVest)
            {
                // Govdenin onune ve arkasina yapisan ince yelek plakalari + cepler.
                builder.AddBox(new Vector3(0f, ChestY + 0.04f, TorsoDepth * 0.53f),
                    new Vector3(TorsoWidth * 0.86f, 0.30f, 0.05f), style.ShirtAccent);
                builder.AddBox(new Vector3(0f, ChestY + 0.04f, -TorsoDepth * 0.53f),
                    new Vector3(TorsoWidth * 0.86f, 0.30f, 0.05f), style.ShirtAccent);
                builder.AddBox(new Vector3(-0.10f, ChestY - 0.04f, TorsoDepth * 0.60f),
                    new Vector3(0.12f, 0.10f, 0.05f), style.Pants);
                builder.AddBox(new Vector3(0.10f, ChestY - 0.04f, TorsoDepth * 0.60f),
                    new Vector3(0.12f, 0.10f, 0.05f), style.Pants);
            }

            builder.SetBone((int)Bone.Neck);
            builder.AddCylinder(new Vector3(0f, NeckY - 0.03f, 0f), 0.075f, 0.075f, 0.09f, 8, style.Skin);
        }

        private static void BuildArms(LowPolyMeshBuilder builder, CharacterStyle style)
        {
            BuildArm(builder, style, -1f, Bone.LeftShoulder);
            BuildArm(builder, style, 1f, Bone.RightShoulder);
        }

        private static void BuildArm(LowPolyMeshBuilder builder, CharacterStyle style, float sign, Bone firstBone)
        {
            int shoulder = (int)firstBone;
            bool sleeveCoversUpperArm = style.Outfit_ != Outfit.TShirt;
            Color upperArmColor = sleeveCoversUpperArm ? style.Shirt : style.Skin;

            // Omuz yuvarlagi: kol ile govde arasindaki boslugu kapatir.
            builder.SetBone(shoulder);
            builder.AddSphere(new Vector3(sign * (TorsoWidth * 0.5f), ShoulderY, 0f),
                LimbThickness * 0.78f, 7, 5, style.Shirt);

            builder.SetBone(shoulder + 1);
            float upperStart = sign * (TorsoWidth * 0.5f);
            float upperEnd = sign * (ArmSpan + 0.22f);
            builder.AddTaperedBox(
                new Vector3((upperStart + upperEnd) * 0.5f, ShoulderY, 0f),
                new Vector3(Mathf.Abs(upperEnd - upperStart), LimbThickness, LimbThickness),
                new Vector2(Mathf.Abs(upperEnd - upperStart), LimbThickness),
                upperArmColor);

            builder.SetBone(shoulder + 2);
            float lowerEnd = sign * (ArmSpan + 0.40f);
            builder.AddTaperedBox(
                new Vector3((upperEnd + lowerEnd) * 0.5f, ShoulderY, 0f),
                new Vector3(Mathf.Abs(lowerEnd - upperEnd), LimbThickness * 0.9f, LimbThickness * 0.9f),
                new Vector2(Mathf.Abs(lowerEnd - upperEnd), LimbThickness * 0.9f),
                style.Skin);

            // El: kucuk kup. Silah socket'i bu kemige bagli.
            builder.SetBone(shoulder + 3);
            builder.AddSphere(new Vector3(sign * (ArmSpan + 0.44f), ShoulderY, 0f),
                LimbThickness * 0.62f, 6, 4, style.Skin);
        }

        private static void BuildHead(LowPolyMeshBuilder builder, CharacterStyle style)
        {
            builder.SetBone((int)Bone.Head);

            float headCenterY = HeadY + HeadRadius * 0.85f;
            Vector3 headCenter = new(0f, headCenterY, 0f);

            // Hafif basik kure: tam kure fazla "top" duruyor, chibi kafa biraz genis olmali.
            builder.AddSphere(headCenter, HeadRadius, 9, 7, style.Skin, 0f, 0,
                new Vector3(1.05f, 1.0f, 0.97f));

            // Kulaklar
            builder.AddSphere(new Vector3(-HeadRadius * 1.0f, headCenterY - 0.01f, 0f),
                HeadRadius * 0.26f, 5, 4, style.Skin);
            builder.AddSphere(new Vector3(HeadRadius * 1.0f, headCenterY - 0.01f, 0f),
                HeadRadius * 0.26f, 5, 4, style.Skin);

            // Gozler: yuzeyden hafif disari tasan koyu kutular (toon stilde en okunakli cozum).
            float eyeZ = HeadRadius * 0.92f;
            float eyeY = headCenterY + 0.015f;
            builder.AddBox(new Vector3(-0.078f, eyeY, eyeZ), new Vector3(0.042f, 0.062f, 0.03f), ToonPalette.EyeDark);
            builder.AddBox(new Vector3(0.078f, eyeY, eyeZ), new Vector3(0.042f, 0.062f, 0.03f), ToonPalette.EyeDark);

            // Kaslar
            builder.AddBox(new Vector3(-0.078f, eyeY + 0.062f, eyeZ - 0.004f),
                new Vector3(0.056f, 0.019f, 0.028f), style.Hair);
            builder.AddBox(new Vector3(0.078f, eyeY + 0.062f, eyeZ - 0.004f),
                new Vector3(0.056f, 0.019f, 0.028f), style.Hair);

            if (style.HasBeard)
            {
                builder.AddTaperedBox(new Vector3(0f, headCenterY - HeadRadius * 0.62f, HeadRadius * 0.62f),
                    new Vector3(0.20f, 0.11f, 0.16f), new Vector2(0.23f, 0.18f), style.Hair);
            }

            BuildHair(builder, style, headCenter);
            BuildHeadGear(builder, style, headCenter);
        }

        private static void BuildHair(LowPolyMeshBuilder builder, CharacterStyle style, Vector3 headCenter)
        {
            if (style.Hair_ == HairStyle.None || style.Gear == HeadGear.Helmet)
                return;

            float capRadius = HeadRadius * (style.Hair_ == HairStyle.Buzz ? 1.015f : 1.06f);
            float lift = style.Hair_ == HairStyle.Buzz ? 0.012f : 0.03f;

            // Kafa kuresinin ustune oturan, biraz daha buyuk ve asagi kaydirilmis ikinci kure:
            // tepeden bakildiginda dogal bir sac kepi verir.
            builder.Push();
            builder.Translate(headCenter + new Vector3(0f, lift, -0.012f));
            builder.AddSphere(Vector3.zero, capRadius, 9, 7, style.Hair, 0f, 0,
                new Vector3(1.03f, 0.92f, 1.0f));
            builder.Pop();

            // Sacin alt yarisini kesmek yerine, yuz bolgesini ten rengiyle tekrar kaplamak
            // hem daha ucuz hem de toon stilde daha temiz duruyor.
            builder.AddSphere(headCenter + new Vector3(0f, -HeadRadius * 0.30f, HeadRadius * 0.16f),
                HeadRadius * 0.86f, 9, 6, style.Skin, 0f, 0, new Vector3(1.0f, 0.78f, 1.0f));

            if (style.Hair_ == HairStyle.Ponytail)
            {
                builder.AddSphere(headCenter + new Vector3(0f, -HeadRadius * 0.15f, -HeadRadius * 1.05f),
                    HeadRadius * 0.42f, 7, 5, style.Hair, 0f, 0, new Vector3(0.8f, 1.3f, 0.8f));
            }
        }

        private static void BuildHeadGear(LowPolyMeshBuilder builder, CharacterStyle style, Vector3 headCenter)
        {
            switch (style.Gear)
            {
                case HeadGear.Cap:
                    builder.AddSphere(headCenter + new Vector3(0f, 0.03f, 0f), HeadRadius * 1.07f, 9, 5,
                        style.GearColor, 0f, 0, new Vector3(1.02f, 0.72f, 1.02f));
                    builder.AddTaperedBox(headCenter + new Vector3(0f, 0.01f, HeadRadius * 1.15f),
                        new Vector3(HeadRadius * 1.5f, 0.035f, HeadRadius * 0.85f),
                        new Vector2(HeadRadius * 1.25f, HeadRadius * 0.85f), style.GearColor);
                    break;

                case HeadGear.Helmet:
                    // Kask: kafayi saran kubbe + on siperlik + cene bandi.
                    builder.AddSphere(headCenter + new Vector3(0f, 0.045f, 0f), HeadRadius * 1.14f, 10, 6,
                        style.GearColor, 0f, 0, new Vector3(1.04f, 1.0f, 1.04f));
                    builder.AddBox(headCenter + new Vector3(0f, 0.055f, HeadRadius * 1.02f),
                        new Vector3(HeadRadius * 1.55f, HeadRadius * 0.62f, 0.06f),
                        ToonPalette.Window);
                    builder.AddBox(headCenter + new Vector3(0f, -HeadRadius * 0.62f, 0f),
                        new Vector3(HeadRadius * 2.1f, 0.045f, HeadRadius * 2.1f),
                        ToonPalette.GunGrip);
                    break;

                case HeadGear.Beanie:
                    builder.AddSphere(headCenter + new Vector3(0f, 0.05f, 0f), HeadRadius * 1.08f, 9, 5,
                        style.GearColor, 0f, 0, new Vector3(1.0f, 0.85f, 1.0f));
                    builder.AddCylinder(headCenter + new Vector3(0f, HeadRadius * 0.05f, 0f),
                        HeadRadius * 1.1f, HeadRadius * 1.1f, 0.07f, 10, ToonPalette.ShirtWhite);
                    break;
            }
        }

        #endregion

        #region Avatar

        /// <summary>
        /// Kemik hiyerarsisinden Humanoid Avatar uretir. Bu adim basarisiz olursa
        /// Mixamo animasyonlari retarget edilemez, o yuzden sonucu mutlaka dogruluyoruz.
        /// </summary>
        private static Avatar BuildAvatar(GameObject root, Transform[] bones)
        {
            Dictionary<HumanBodyBones, Bone> mapping = new()
            {
                { HumanBodyBones.Hips, Bone.Hips },
                { HumanBodyBones.Spine, Bone.Spine },
                { HumanBodyBones.Chest, Bone.Chest },
                { HumanBodyBones.Neck, Bone.Neck },
                { HumanBodyBones.Head, Bone.Head },
                { HumanBodyBones.LeftShoulder, Bone.LeftShoulder },
                { HumanBodyBones.LeftUpperArm, Bone.LeftUpperArm },
                { HumanBodyBones.LeftLowerArm, Bone.LeftLowerArm },
                { HumanBodyBones.LeftHand, Bone.LeftHand },
                { HumanBodyBones.RightShoulder, Bone.RightShoulder },
                { HumanBodyBones.RightUpperArm, Bone.RightUpperArm },
                { HumanBodyBones.RightLowerArm, Bone.RightLowerArm },
                { HumanBodyBones.RightHand, Bone.RightHand },
                { HumanBodyBones.LeftUpperLeg, Bone.LeftUpperLeg },
                { HumanBodyBones.LeftLowerLeg, Bone.LeftLowerLeg },
                { HumanBodyBones.LeftFoot, Bone.LeftFoot },
                { HumanBodyBones.LeftToes, Bone.LeftToes },
                { HumanBodyBones.RightUpperLeg, Bone.RightUpperLeg },
                { HumanBodyBones.RightLowerLeg, Bone.RightLowerLeg },
                { HumanBodyBones.RightFoot, Bone.RightFoot },
                { HumanBodyBones.RightToes, Bone.RightToes }
            };

            List<HumanBone> humanBones = new();
            foreach (KeyValuePair<HumanBodyBones, Bone> pair in mapping)
            {
                humanBones.Add(new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)pair.Key],
                    boneName = bones[(int)pair.Value].name,
                    limit = new HumanLimit { useDefaultValues = true }
                });
            }

            List<SkeletonBone> skeleton = new()
            {
                new SkeletonBone
                {
                    name = root.name,
                    position = Vector3.zero,
                    rotation = Quaternion.identity,
                    scale = Vector3.one
                }
            };

            foreach (Transform bone in bones)
            {
                skeleton.Add(new SkeletonBone
                {
                    name = bone.name,
                    position = bone.localPosition,
                    rotation = bone.localRotation,
                    scale = bone.localScale
                });
            }

            HumanDescription description = new()
            {
                human = humanBones.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = $"{root.name}_Avatar";

            if (!avatar.isValid)
            {
                Debug.LogError(
                    $"ArtGen: {root.name} icin Humanoid Avatar gecersiz uretildi. " +
                    "Mixamo animasyonlari bu karaktere retarget edilemez."
                );
            }

            return avatar;
        }

        /// <summary>
        /// Avatar'i diske yazar. Zaten varsa icerigini kopyalar; boylece prefab'in
        /// Animator'undaki Avatar referansi her yeniden uretimde kopmaz.
        /// </summary>
        private static Avatar SaveAvatar(Avatar avatar, string characterName)
        {
            ArtGenIO.EnsureFolder(ArtGenIO.AvatarFolder);
            string path = $"{ArtGenIO.AvatarFolder}/Character_{characterName}_Avatar.asset";

            Avatar existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(avatar, path);
                return avatar;
            }

            EditorUtility.CopySerialized(avatar, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(avatar);
            return existing;
        }

        #endregion
    }
}
