using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        [Header("First axe")]
        [Min(1)] public int FirstAxeWoodCost = 3;
        [Min(.1f)] public float WoodPickupRadius = 1.2f;
        [Min(.5f)] public float BareHandRange = 1.4f;
        [Min(.3f)] public float BareHandSeconds = .55f;
        [Min(.14f)] public float BareHandContact = .22f;
        [Min(1)] public int SmallTreeHealth = 3, NormalTreeHealth = 6, LargeTreeHealth = 9;
        [Min(1)] public int SmallTreeWood = 3, NormalTreeWood = 4, LargeTreeWood = 5;
        [Min(1)] public int BareHandDamage = 1, AxeTreeDamage = 3;

        [Header("Distance driven gait")]
        [Min(.1f)] public float FootfallDistance = 1.6f;
        [Min(0)] public float WalkLift = .14f, WalkSway = .025f;

        readonly Being[] firstAxeTeachingTrees = new Being[2];
        readonly Dictionary<Being, int> treeWood = new Dictionary<Being, int>();
        readonly List<Transform> looseWood = new List<Transform>();
        readonly HashSet<Transform> antlerDrops = new HashSet<Transform>();
        readonly HashSet<Transform> featherDrops = new HashSet<Transform>();
        int carriedAntler, carriedFeather;
        readonly HashSet<Being> harvestedTrees = new HashSet<Being>();
        readonly Dictionary<Being, TreeKnock> treeKnocks = new Dictionary<Being, TreeKnock>();

        struct TreeKnock
        {
            public Vector3 direction;
            public float began, angle, duration;
        }

        int carriedWood;
        bool woodenAxe;
        float gaitDistance, walkBlend;
        Vector3 gaitDirection = Vector3.right;
        Being firstAxeStrikeTarget;
        Vector3 firstAxeStrikePoint, firstAxeArmTip;
        int firstAxeImpactFrame = -1;
        AudioSource impactAudio;
        AudioClip impactClip;
        Sprite woodSprite;
        Transform firstAxeUpper;
        LineRenderer firstAxeArm, farArm, leftLeg, rightLeg;
        SpriteRenderer firstAxeFist, firstAxeHead, firstAxeChest;
        Material firstAxeInk;

        bool FirstAxeActive => riversideBoar != null;
        bool CanCraftFirstAxe => FirstAxeActive && !woodenAxe && attackAge < 0
            && carriedWood >= Mathf.Max(1, FirstAxeWoodCost);
        bool IsFirstAxeTeachingTree(Being tree) => tree != null
            && (tree == firstAxeTeachingTrees[0] || tree == firstAxeTeachingTrees[1]);

        void SetupFirstAxe()
        {
            // Use the two clear positions near the father without moving any of the existing forest.
            firstAxeTeachingTrees[0] = AddDictionaryPlant(new Vector3(-11, 0, -3), true, 1200, "plant_01", 2.6f);
            firstAxeTeachingTrees[1] = AddDictionaryPlant(new Vector3(-11, 0, 1.2f), true, 1201, "plant_01", 2.6f);
            foreach (var tree in plants)
            {
                if (!tree.tree || !tree.root.gameObject.activeSelf) continue;
                float height = tree.fullGlyph.bounds.size.y * tree.initialScale.y / Mathf.Max(1, XRArtworkScale);
                bool small = IsFirstAxeTeachingTree(tree);
                bool large = height >= 3.45f;
                tree.health = small ? SmallTreeHealth : large ? LargeTreeHealth : NormalTreeHealth;
                treeWood[tree] = small ? SmallTreeWood : large ? LargeTreeWood : NormalTreeWood;
            }

            BuildFirstAxeRig();
            impactAudio = player.gameObject.AddComponent<AudioSource>();
            impactAudio.playOnAwake = false;
            var samples = new float[3308];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (Mathf.Sin(i * .061f) + .24f * Mathf.Sin(i * .164f))
                    * Mathf.Exp(-i / 450f) * .38f;
            impactClip = AudioClip.Create("Synthesized wood impact", samples.Length, 1, 22050, false);
            impactClip.SetData(samples, 0);
            fatherIntroActive = false;
            notice = "";
            noticeTime = 0;
        }

        bool TryCraftFirstAxe()
        {
            if (!CanCraftFirstAxe) return false;
            carriedWood -= Mathf.Max(1, FirstAxeWoodCost);
            woodenAxe = true;
            selectedSlot = 0;
            AdvanceRiverTask(RiverTask.Trees);
            BeginManualStoryDialogue(new[]{"Father: Cut a few more trees.", "Son: Don't we have enough wood?",
                "Father: Enough for us. We can sell the rest.", "Son: All right."});
            Tell("Wooden axe crafted.");
            return true;
        }

        void UpdateFirstAxe(Vector3 previousFeet, float dt)
        {
            if (!FirstAxeActive) return;
            Vector3 displacement = Vector3.ProjectOnPlane(Feet - previousFeet, Vector3.up);
            float distance = displacement.magnitude;
            bool walked = XRExperienceReady && jumpAge < 0 && attackAge < 0 && distance > .0001f && distance < 1;
            if (walked)
            {
                gaitDirection = displacement / distance;
                gaitDistance += distance;
            }
            walkBlend = Mathf.MoveTowards(walkBlend, walked ? 1 : 0, dt / .12f);

        }

        Transform NearestWoodPickup()
        {
            if (!FirstAxeActive || !XRExperienceReady) return null;
            Transform closest = null;
            float nearest = WoodPickupRadius * WoodPickupRadius;
            foreach (var wood in looseWood)
            {
                if (wood == null) continue;
                float distance = (Feet - wood.position).sqrMagnitude;
                if (distance > nearest || (closest != null && distance == nearest)) continue;
                closest = wood;
                nearest = distance;
            }
            return closest;
        }

        bool TryPickUpWood()
        {
            var wood = NearestWoodPickup();
            if (wood == null) return false;
            looseWood.Remove(wood);
            wood.gameObject.SetActive(false);
            Destroy(wood.gameObject);
            if (wood == riverStone)
            {
                carriedStone++;
                Tell("Stone collected.");
                return true;
            }
            if (antlerDrops.Remove(wood))
            {
                carriedAntler++;
                Tell($"Antler {carriedAntler}");
                return true;
            }
            if (featherDrops.Remove(wood))
            {
                carriedFeather++;
                RiverFeatherCollected();
                Tell($"Feather {carriedFeather} · Trade Goods");
                return true;
            }
            carriedWood++;
            Tell(woodenAxe ? $"Wood {carriedWood}" : $"Wood {carriedWood}/{FirstAxeWoodCost}");
            return true;
        }

        void UpdateFirstAxeAttack(float dt)
        {
            axe.gameObject.SetActive(XRExperienceReady && HasCuttingTool);
            if (!XRExperienceReady)
            {
                attackAge = -1;
                firstAxeStrikeTarget = null;
                return;
            }
            if (attackAge < 0)
            {
                if (HasCuttingTool) SetAxePose(-25f);
                return;
            }

            float duration = HasCuttingTool ? Mathf.Max(.28f, AxeInterval) : Mathf.Max(.3f, BareHandSeconds);
            float contact = HasCuttingTool ? .25f : Mathf.Clamp(BareHandContact, .14f, duration - .04f);
            if (attackAge == 0)
            {
                firstAxeStrikeTarget = FindFirstAxeTarget();
                firstAxeStrikePoint = FirstAxeContactPoint(firstAxeStrikeTarget);
                Vector3 lineToTarget = firstAxeStrikePoint - Feet;
                var right = FirstAxeCardRight();
                float side = Vector3.Dot(lineToTarget, right);
                if (Mathf.Abs(side) > .08f) facing = side < 0 ? -1 : 1;
            }

            // Keep one drawn frame at full extension even when a slow frame crosses impact.
            attackAge += dt;
            if (!hit && attackAge >= contact)
            {
                attackAge = contact;
                hit = true;
                firstAxeImpactFrame = Time.frameCount;
                if (IsFirstAxeTargetInReach(firstAxeStrikeTarget, out _))
                    ResolveFirstAxeStrike(firstAxeStrikeTarget);
            }

            if (HasCuttingTool)
            {
                float swing = attackAge < .16f
                    ? Mathf.Lerp(20, 85, Mathf.SmoothStep(0, 1, attackAge / .16f))
                    : Mathf.Lerp(85, -110, Mathf.SmoothStep(0, 1, (attackAge - .16f) / .3f));
                SetAxePose(swing);
            }
            if (attackAge < duration) return;
            attackAge = -1;
            firstAxeStrikeTarget = null;
        }

        void SetAxePose(float angle)
        {
            axe.localPosition = Vector3.zero;
            axe.localRotation = Quaternion.Euler(0, 0, facing * angle);
            var scale = axe.localScale;
            scale.x = Mathf.Abs(scale.x) * facing;
            axe.localScale = scale;
        }

        Vector3 FirstAxeCardRight()
        {
            if (!XRTrackingControlsCamera || cam == null) return body.right;
            Vector3 away = Vector3.ProjectOnPlane(body.position - cam.transform.position, Vector3.up);
            return away.sqrMagnitude > .0001f ? Vector3.Cross(Vector3.up, away.normalized) : body.right;
        }

        Vector3 FirstAxeContactPoint(Being target)
        {
            float height = 42f / 102 * playerImage.sprite.rect.height / playerImage.sprite.pixelsPerUnit
                * body.lossyScale.y;
            Vector3 forward = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
            if (target == null) return Feet + forward * BareHandRange + Vector3.up * height;
            Vector3 root = target.tree ? target.home : target.root.position;
            Vector3 toward = Vector3.ProjectOnPlane(root - Feet, Vector3.up).normalized;
            float trunkSurface = target.tree ? .09f : .32f;
            return root - toward * trunkSurface + Vector3.up * height;
        }

        bool IsFirstAxeTargetInReach(Being target, out float distance)
        {
            distance = 0;
            if (target == null || target.health <= 0 || !target.root.gameObject.activeInHierarchy || (target.animal && !target.renderer.enabled)) return false;
            Vector3 targetPosition = target.tree ? target.home : target.root.position;
            Vector3 delta = Vector3.ProjectOnPlane(targetPosition - Feet, Vector3.up);
            distance = delta.magnitude;
            float reach = HasCuttingTool ? (selectedSlot == 1 ? .85f : AxeRangeSteps) * RiversideStep : BareHandRange;
            return distance <= reach && Vector3.Dot(delta.normalized, heading.normalized) > .15f;
        }

        Being FindFirstAxeTarget()
        {
            Being closest = null;
            float nearest = float.PositiveInfinity;
            foreach (var tree in plants)
            {
                if (!tree.tree || !IsFirstAxeTargetInReach(tree, out float distance) || distance >= nearest) continue;
                closest = tree;
                nearest = distance;
            }
            // Wild animals become targets after the first tool is equipped.
            if (HasCuttingTool)
                foreach (var animal in animals)
                {
                    if (!RiversideAnimal(animal) || !IsFirstAxeTargetInReach(animal, out float distance) || distance >= nearest) continue;
                    closest = animal;
                    nearest = distance;
                }
            return closest;
        }

        void StrikeFirstAxeTarget()
        {
            var target = FindFirstAxeTarget();
            if (target != null) ResolveFirstAxeStrike(target);
        }

        void ResolveFirstAxeStrike(Being target)
        {
            if (target.animal)
            {
                HitRiverside(target);
                return;
            }

            impactAudio.PlayOneShot(impactClip);
            var controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (controller.TryGetHapticCapabilities(out var capability) && capability.supportsImpulse)
                controller.SendHapticImpulse(0, .18f, .08f);

            int damage = Mathf.Max(1, HasCuttingTool ? AxeTreeDamage : BareHandDamage);
            for (int i = 0; i < damage && target.health > 0; i++) ApplyEcologyHit(target);
            var away = Vector3.ProjectOnPlane(target.home - Feet, Vector3.up).normalized;
            if (away == Vector3.zero) away = Vector3.right;
            float lean = IsFirstAxeTeachingTree(target) && target.health == 1 ? 14 : 5;
            treeKnocks[target] = new TreeKnock { direction = away, began = clock, angle = lean, duration = .32f };
            int leafCount = target.health <= 0 ? 7 : target.health == 1 ? 4 : 2;
            for (int i = 0; i < leafCount; i++) SpawnFallingLeaf(target, i % 6);
            if (target.health > 0 || !harvestedTrees.Add(target)) return;
            RiverTreeFelled(target);

            int woodCount = treeWood.TryGetValue(target, out var configuredCount) ? configuredCount : NormalTreeWood;
            DropFirstAxeWood(target.home, away, Mathf.Max(1, woodCount));
        }

        bool ApplyFirstAxeTreeTilt(Being tree)
        {
            if (!treeKnocks.TryGetValue(tree, out var knock)) return false;
            bool fallen = tree.health <= 0;
            float progress = Mathf.Clamp01((clock - knock.began) / knock.duration);
            float angle = fallen
                ? 82 * Mathf.SmoothStep(0, 1, tree.fall)
                : Mathf.Sin(progress * Mathf.PI) * knock.angle;
            if (fallen && tree.fall >= 1) angle = 0;
            var axis = Vector3.Cross(Vector3.up, knock.direction);
            if (tree.art.TryGetComponent<XRUprightBillboard>(out var card))
                card.SetWorldTilt(Quaternion.AngleAxis(angle, axis));
            if (!fallen && progress >= 1) treeKnocks.Remove(tree);
            return true;
        }

        void DropFirstAxeWood(Vector3 trunk, Vector3 away, int count)
        {
            if (woodSprite == null) woodSprite = DrawWoodBranch();
            var assigned = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 position = trunk;
                float angle = count <= 1 ? 0 : Mathf.Lerp(-65, 65, (float)i / (count - 1));
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    float offset = attempt == 0 ? 0 : (attempt % 2 == 0 ? -1 : 1) * ((attempt + 1) / 2) * 17;
                    float radius = 1.2f + (attempt % 3) * .48f;
                    var candidate = trunk + Quaternion.AngleAxis(angle + offset, Vector3.up) * away * radius;
                    if (River(candidate) || !Free(candidate)) continue;
                    if (assigned.Exists(other => Vector3.Distance(other, candidate) < .65f)) continue;
                    if (looseWood.Exists(other => Vector3.Distance(other.position, candidate) < .45f)) continue;
                    position = candidate;
                    break;
                }
                assigned.Add(position);
                var wood = Billboard("Dropped wood log / game artwork", null, woodSprite, .30f);
                wood.position = position;
                wood.GetComponent<SpriteRenderer>().sortingOrder = 101 - Mathf.RoundToInt(position.z * 10);
                looseWood.Add(wood);
            }
        }

        static Sprite MakeWoodSprite()
        {
            const int width = 64, height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Log artwork";
            var pixels = new Color32[width * height];
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                float z = y - 15f - (x - 25) * .10f;
                float cap = Mathf.Sqrt(Mathf.Pow((x - 51) / 10f, 2) + Mathf.Pow(z / 11f, 2));
                bool bark = x > 6 && x <= 51 && Mathf.Abs(z) < 9.25f;
                if (!bark && cap > 1) continue;
                var color = new Color32(130, 77, 41, 255);
                if (bark && (Mathf.Abs(z) > 7.7f || Mathf.Abs(z + 2.5f + Mathf.Sin(x * .23f)) < .75f))
                    color = new Color32(77, 43, 27, 255);
                if (cap <= 1)
                    color = cap > .87f ? new Color32(75, 45, 28, 255)
                        : Mathf.Abs(cap - .50f) < .065f ? new Color32(127, 79, 44, 255)
                        : new Color32(224, 160, 91, 255);
                pixels[x + y * width] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, 0), height);
        }
    }
}
