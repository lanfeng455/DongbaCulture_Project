using System.Collections;
using System.IO;
using UnityEngine;

namespace DongbaPrototype
{
    public sealed partial class ForestPrototype
    {
        public enum EcologyOutcome { Balance, Flourishing, Withering }
        public EcologyOutcome Ecology { get; private set; } = EcologyOutcome.Balance;
        public int CareActions { get; private set; }
        public int HarmActions { get; private set; }

        readonly int[] experienceTreeHealth = { 2, 2, 2 };
        readonly float[] experienceTreeHitTimer = new float[3];
        readonly float[] experienceTreeHitDirection = new float[3];
        readonly int[] experiencePlantStage = { 0, 1, 0 };
        readonly float[] experiencePlantGrowth = { 0f, .52f, 0f };
        static readonly float[] ExperiencePlantWorldX = { 3.86f, 4.81f, 12.88f };

        float ecologyVisual;
        float ecologyNoticeTimer;
        float interactionNoticeTimer;
        float feedEffectTimer;
        float offspringTimer;
        float animalPopulationVisual = 1f;
        float abundanceEffectTimer;
        float tutorialHintTimer = 9f;
        int animalFeeds;
        bool offspringVisible;
        bool abundanceCelebrated;
        string experienceMessage = "";

        struct NatureMote { public Vector2 position, velocity; public float age, lifetime, size; public bool active; }
        readonly NatureMote[] natureMotes = new NatureMote[42];
        // Plants grow in loose edge clusters while animals occupy the gaps.
        // Keeping their silhouettes apart preserves readability at full abundance.
        static readonly float[] LocalPlantOffsets = { -32f, 29f, -46f, 43f };
        static readonly float[] ExtraAnimalX = { 2.37f, 3.66f, 5.17f, 13.90f, 17.97f, 19.05f };
        bool experiencePreview;
        bool animalProvoked;

        bool ExperienceAnimalFriendly => animalFeeds >= 2 && Hits < 3;
        float ExperienceMotionScale => Mathf.Lerp(.42f, 1.12f, Mathf.InverseLerp(-1f, 1f, ecologyVisual)) + AbundancePulse * .32f;
        float AbundancePulse => abundanceEffectTimer <= 0 ? 0 : Mathf.Sin(Mathf.Clamp01(abundanceEffectTimer / 4f) * Mathf.PI) * Mathf.Clamp01(abundanceEffectTimer);

        void UpdateExperience(float dt)
        {
            ecologyNoticeTimer = Mathf.Max(0, ecologyNoticeTimer - dt);
            interactionNoticeTimer = Mathf.Max(0, interactionNoticeTimer - dt);
            feedEffectTimer = Mathf.Max(0, feedEffectTimer - dt);
            abundanceEffectTimer = Mathf.Max(0, abundanceEffectTimer - dt);
            tutorialHintTimer = Mathf.Max(0, tutorialHintTimer - dt);
            for (int i = 0; i < experienceTreeHitTimer.Length; i++)
                experienceTreeHitTimer[i] = Mathf.Max(0, experienceTreeHitTimer[i] - dt);

            for (int i = 0; i < experiencePlantGrowth.Length; i++)
            {
                float target = experiencePlantStage[i] == 0 ? 0 : experiencePlantStage[i] == 1 ? .48f : experiencePlantStage[i] == 2 ? .76f : 1f;
                experiencePlantGrowth[i] = Mathf.MoveTowards(experiencePlantGrowth[i], target, dt * .32f);
            }

            float targetEcology = Mathf.Clamp((CareActions - HarmActions) / 4f, -1f, 1f);
            ecologyVisual = Mathf.MoveTowards(ecologyVisual, targetEcology, dt * .38f);
            EcologyOutcome previous = Ecology;
            Ecology = targetEcology >= .55f ? EcologyOutcome.Flourishing : targetEcology <= -.55f ? EcologyOutcome.Withering : EcologyOutcome.Balance;
            if (previous != Ecology)
            {
                ecologyNoticeTimer = 0;
            }

            if (ExperienceAnimalFriendly && ecologyVisual > .25f)
            {
                offspringTimer += dt;
                if (offspringTimer >= 4f) offspringVisible = true;
            }
            else
            {
                offspringTimer = 0;
                if (!ExperienceAnimalFriendly) offspringVisible = false;
            }

            int maturePlants = MatureExperiencePlants();
            int targetPopulation = ExperienceAnimalFriendly
                ? Mathf.Clamp(1 + animalFeeds + maturePlants + Mathf.Max(0, (CareActions - HarmActions) / 2), 1, ExtraAnimalX.Length + 1)
                : 1;
            animalPopulationVisual = Mathf.MoveTowards(animalPopulationVisual, targetPopulation, dt * .72f);

            int extraPlants = ExtraExperiencePlantCount();
            if (!abundanceCelebrated && Ecology == EcologyOutcome.Flourishing && extraPlants >= 8 && animalPopulationVisual >= 5f)
            {
                abundanceCelebrated = true;
                abundanceEffectTimer = 4f;
                impactWind = 1.25f;
                SpawnNatureMotes();
            }
            for (int i = 0; i < natureMotes.Length; i++) if (natureMotes[i].active)
            {
                NatureMote mote = natureMotes[i];
                mote.age += dt;
                if (mote.age >= mote.lifetime) mote.active = false;
                else
                {
                    mote.position += mote.velocity * dt;
                    mote.velocity.x += (9f + AbundancePulse * 10f) * dt;
                    mote.velocity.y += 2.5f * dt;
                }
                natureMotes[i] = mote;
            }

            if (music != null)
                music.volume = MusicVolume * (Mathf.Lerp(.46f, 1.08f, Mathf.InverseLerp(-1f, 1f, ecologyVisual)) + AbundancePulse * .22f);
        }

        void InteractExperience()
        {
            if (Busy || State == Phase.Defeat) return;

            if (Hits < 3 && Mathf.Abs(X - animalWorldX) <= 1.75f && Y < 1.25f)
            {
                if (animalFeeds < 3)
                {
                    animalFeeds++;
                    CareActions++;
                }
                animalAttackWindup = 0;
                animalAttackCooldown = 2.2f;
                feedEffectTimer = .85f;
                if (animalFeeds >= 2 && Hits > 0) Hits--;
                if (animalFeeds >= 2) animalProvoked = false;
                State = Phase.Explore;
                return;
            }

            int plant = NearestExperiencePlant(1.05f);
            if (plant >= 0)
            {
                if (experiencePlantStage[plant] < 3)
                {
                    experiencePlantStage[plant]++;
                    CareActions++;
                    SpawnInk(new Vector2(50 + ExperiencePlantWorldX[plant] * 59, 530), 4, .34f, false);
                }
                return;
            }
        }

        int NearestExperiencePlant(float range)
        {
            int result = -1;
            float best = range;
            for (int i = 0; i < ExperiencePlantWorldX.Length; i++)
            {
                float distance = Mathf.Abs(X - ExperiencePlantWorldX[i]);
                if (distance < best)
                {
                    best = distance;
                    result = i;
                }
            }
            return result;
        }

        int MatureExperiencePlants()
        {
            int count = 0;
            for (int i = 0; i < experiencePlantStage.Length; i++) if (experiencePlantStage[i] >= 2) count++;
            return count;
        }

        int ExtraExperiencePlantCount()
        {
            int count = 0;
            for (int site = 0; site < experiencePlantStage.Length; site++)
                count += LocalPlantCount(site);
            return count;
        }

        int LocalPlantCount(int site)
        {
            return experiencePlantStage[site] < 2 ? 0 : experiencePlantStage[site] == 2 ? 2 : 4;
        }

        void SpawnNatureMotes()
        {
            for (int i = 0; i < natureMotes.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                natureMotes[i] = new NatureMote
                {
                    active = true,
                    age = 0,
                    lifetime = 2.2f + (i % 7) * .22f,
                    size = 2.2f + (i % 5) * .65f,
                    position = new Vector2(70f + (i * 83f) % 1140f, 500f + (i % 6) * 12f),
                    velocity = new Vector2(side * (5f + (i % 4) * 2f), -18f - (i % 8) * 3f)
                };
            }
        }

        bool UpdateFriendlyExperienceAnimal(float dt)
        {
            if (!ExperienceAnimalFriendly) return false;
            animalAttackWindup = 0;
            animalAttackCooldown = Mathf.Max(animalAttackCooldown, .4f);
            animalPatrolTime += dt;
            float followBias = Mathf.Abs(X - animalWorldX) < 4f ? Mathf.Clamp(X + 1.1f, 13.5f, 18.2f) : AnimalX;
            float target = followBias + Mathf.Sin(animalPatrolTime * .9f) * .42f;
            animalFacing = target >= animalWorldX ? 1 : -1;
            animalMoveVelocity = Mathf.MoveTowards(animalMoveVelocity, animalFacing * .72f, dt * 2.6f);
            if (Mathf.Abs(target - animalWorldX) < .08f) animalMoveVelocity *= .35f;
            animalWorldX = Mathf.Clamp(animalWorldX + animalMoveVelocity * dt, 13.35f, 18.4f);
            return true;
        }

        bool TryStrikeExperienceTree()
        {
            if (automated && !experiencePreview) return false;
            for (int i = 0; i < experienceTreeHealth.Length; i++)
            {
                if (experienceTreeHealth[i] <= 0 || !AxeBladeOverlapsExperienceTree(i)) continue;
                experienceTreeHealth[i]--;
                HarmActions++;
                if (experienceTreeHealth[i] == 0) HarmActions++;
                experienceTreeHitTimer[i] = .28f;
                experienceTreeHitDirection[i] = attackFacing;
                hitStopRemaining = .045f;
                environmentFreeze = .045f;
                cameraShakeAmplitude = experienceTreeHealth[i] == 0 ? 3.4f : 1.6f;
                cameraShakeDuration = cameraShakeTimer = .1f;
                Rect tree = ambientRects[i];
                SpawnInk(new Vector2(tree.center.x, tree.yMax - 30), experienceTreeHealth[i] == 0 ? 9 : 5, .55f, true);
                sound.PlayOneShot(strikes[0]);
                interactionNoticeTimer = 1.35f;
                experienceMessage = experienceTreeHealth[i] == 0 ? "A TREE FALLS SILENT" : "THE TREE BEARS THE CUT";
                return true;
            }
            return false;
        }

        bool AxeBladeOverlapsExperienceTree(int index)
        {
            float attackLean = attackFacing * AttackLeanDegrees * impact;
            Vector2 playerFoot = new Vector2(50 + X * 59 + impact * attackFacing * AttackLungePixels, 536 - Y * 59);
            GetAttackAxePose(playerFoot, impact, attackLean, out Vector2 hand, out float size, out float degrees);
            SetBladePoint(0, .50f, .25f, hand, size, degrees);
            SetBladePoint(1, .91f, .31f, hand, size, degrees);
            SetBladePoint(2, .72f, .56f, hand, size, degrees);
            SetBladePoint(3, .50f, .64f, hand, size, degrees);
            Rect tree = ambientRects[index];
            Rect trunk = new Rect(tree.center.x - 20f, tree.yMax - 112f, 40f, 112f);
            return ConvexPolygonOverlapsRect(axeBladeCollider, trunk);
        }

        void RecordExperienceAnimalHarm(bool defeated)
        {
            HarmActions += defeated ? 2 : 1;
            animalProvoked = !defeated;
            animalFeeds = 0;
            offspringVisible = false;
            offspringTimer = 0;
        }

        void ResetExperience()
        {
            CareActions = HarmActions = animalFeeds = 0;
            Ecology = EcologyOutcome.Balance;
            ecologyVisual = ecologyNoticeTimer = interactionNoticeTimer = feedEffectTimer = offspringTimer = abundanceEffectTimer = 0;
            animalPopulationVisual = 1f;
            tutorialHintTimer = 9f;
            offspringVisible = false;
            abundanceCelebrated = false;
            animalProvoked = false;
            experienceMessage = "";
            for (int i = 0; i < natureMotes.Length; i++) natureMotes[i].active = false;
            for (int i = 0; i < experienceTreeHealth.Length; i++)
            {
                experienceTreeHealth[i] = 2;
                experienceTreeHitTimer[i] = experienceTreeHitDirection[i] = 0;
            }
            experiencePlantStage[0] = 0;
            experiencePlantStage[1] = 1;
            experiencePlantStage[2] = 0;
            experiencePlantGrowth[0] = 0;
            experiencePlantGrowth[1] = .52f;
            experiencePlantGrowth[2] = 0;
        }

        bool ExperienceAmbientVisible(int index)
        {
            if (index < 3) return experienceTreeHealth[index] > 0;
            return experiencePlantGrowth[index - 3] > .01f;
        }

        Rect ExperienceAmbientRect(int index, Rect source)
        {
            if (index < 3) return source;
            float growth = experiencePlantGrowth[index - 3];
            return new Rect(source.center.x - source.width * growth * .5f, source.yMax - source.height * growth, source.width * growth, source.height * growth);
        }

        Color ExperienceAmbientTint(int index)
        {
            float life = Mathf.InverseLerp(-1f, 1f, ecologyVisual);
            Color tint = Color.Lerp(new Color(.66f, .57f, .47f, .58f), Color.white, life);
            if (index < 3 && experienceTreeHealth[index] == 1) tint = Color.Lerp(tint, new Color(.72f, .48f, .34f, .82f), .34f);
            if (index < 3 && experienceTreeHitTimer[index] > 0) tint = Color.Lerp(tint, new Color(.72f, .28f, .18f, 1f), experienceTreeHitTimer[index] / .28f * .45f);
            return tint;
        }

        float ExperienceTreeImpact(int index)
        {
            float timer = experienceTreeHitTimer[index];
            if (timer <= 0) return 0;
            float p = 1f - timer / .28f;
            return experienceTreeHitDirection[index] * Mathf.Sin(p * Mathf.PI * 2.4f) * (1f - p) * 13f;
        }

        void DrawExperienceEnvironmentWash()
        {
            if (ecologyVisual < -.02f)
                Box(new Rect(0, 0, 1280, 720), new Color(.29f, .25f, .20f, -ecologyVisual * .13f));
            else if (ecologyVisual > .02f)
                Box(new Rect(0, 0, 1280, 720), new Color(.72f, .78f, .49f, ecologyVisual * .045f + AbundancePulse * .035f));
        }

        void DrawExperienceGrowth()
        {
            float masterPhase = ambientClock * Mathf.PI * 2 / MasterPeriod;
            for (int site = 0; site < experiencePlantStage.Length; site++)
            for (int local = 0; local < LocalPlantCount(site); local++)
            {
                Texture2D texture = ambientArt[3 + site];
                if (texture == null) continue;
                float growth = Mathf.InverseLerp(.48f, 1f, experiencePlantGrowth[site]);
                float scale = (.38f + local * .025f) * growth;
                float height = (site == 2 ? 76f : 112f) * scale;
                float width = 48f * scale;
                float x = 50 + ExperiencePlantWorldX[site] * 59 + LocalPlantOffsets[local];
                // Riverbank plants spread inland, never across the water.
                if (site == 2) x = 50 + ExperiencePlantWorldX[site] * 59 + 20 + local * 18;
                Rect rect = new Rect(x - width * .5f, 528f - height, width, height);
                float wave = Mathf.Sin(masterPhase - site * .17f - local * .12f) * 2.5f * growth;
                DrawBent(texture, rect, wave, 10, new Color(.88f, .91f, .75f, .66f));
            }

            int extraAnimals = Hits < 3 ? Mathf.Clamp(Mathf.FloorToInt(animalPopulationVisual) - 1, 0, ExtraAnimalX.Length) : 0;
            for (int i = 0; i < extraAnimals; i++)
            {
                float scale = .40f + (i % 3) * .045f;
                float footX = 50 + ExtraAnimalX[i] * 59;
                float hop = Mathf.Abs(Mathf.Sin(clock * (2.1f + i * .08f) + i * .73f)) * (1.2f + AbundancePulse * 3.6f);
                Vector2 foot = new Vector2(footX, 536 - hop);
                Rect rect = RectFromFoot(foot, 102 * ChickenTextureWidthRatio * scale, 91 * ChickenTextureHeightRatio * scale, ChickenFootPivotY);
                DrawArtPivoted(animalArt, 4, rect, foot, Color.white, i % 2 == 0, 0);
            }
        }

        void DrawExperienceWorld()
        {
            for (int i = 0; i < experiencePlantStage.Length; i++)
            {
                if (experiencePlantStage[i] != 0) continue;
                float x = 50 + ExperiencePlantWorldX[i] * 59;
                Box(new Rect(x - 13, 535, 26, 2), new Color(.31f, .23f, .17f, .34f));
                Box(new Rect(x - 5, 530, 10, 2), new Color(.31f, .23f, .17f, .22f));
            }

            if (feedEffectTimer > 0 && Hits < 3)
            {
                float progress = 1 - feedEffectTimer / .85f;
                Vector2 start = new Vector2(50 + X * 59, 500 - Y * 59);
                Vector2 end = new Vector2(50 + animalWorldX * 59, 520);
                for (int i = 0; i < 5; i++)
                {
                    float t = Mathf.Repeat(progress + i * .14f, 1f);
                    Vector2 point = Vector2.Lerp(start, end, t);
                    point.y -= Mathf.Sin(t * Mathf.PI) * 22f;
                    float size = 4f - i * .35f;
                    Box(new Rect(point.x - size * .5f, point.y - size * .5f, size, size), new Color(.45f, .24f, .13f, 1f - t * .55f));
                }
            }

            for (int i = 0; i < natureMotes.Length; i++) if (natureMotes[i].active)
            {
                NatureMote mote = natureMotes[i];
                float alpha = 1f - mote.age / mote.lifetime;
                Color color = i % 3 == 0 ? new Color(.54f, .24f, .14f, alpha * .58f) : new Color(.24f, .36f, .19f, alpha * .48f);
                Box(new Rect(mote.position.x, mote.position.y, mote.size * 1.7f, mote.size), color);
            }

            DrawExperiencePrompt();
        }

        void DrawExperiencePrompt()
        {
            if (Busy || State == Phase.Defeat) return;
            string prompt = "";
            float x = 0;
            float y = 0;
            if (Hits < 3 && Mathf.Abs(X - animalWorldX) <= 1.75f && Y < 1.25f)
            {
                prompt = ExperienceAnimalFriendly ? "FEED" : "OFFER";
                x = 50 + animalWorldX * 59;
                y = 408;
            }
            else
            {
                int plant = NearestExperiencePlant(1.05f);
                if (plant >= 0)
                {
                    prompt = experiencePlantStage[plant] == 0 ? "PLANT" : experiencePlantStage[plant] < 3 ? "TEND" : "";
                    x = 50 + ExperiencePlantWorldX[plant] * 59;
                    y = 470;
                }
            }
            if (prompt.Length == 0) return;
            DrawInkInteraction(new Vector2(x, y + 11), prompt);
        }

        void DrawExperienceHud()
        {
            if (tutorialHintTimer <= 0) return;
            float alpha = Mathf.Clamp01(tutorialHintTimer / 2f);
            Color ink = new Color(Ink.r, Ink.g, Ink.b, alpha * .72f);
            Color red = new Color(Red.r, Red.g, Red.b, alpha * .66f);
            Text(new Rect(48, 678, 470, 24), "A D  MOVE      SPACE  JUMP      J  AXE      E  INTERACT", small, ink);
            DrawUiStroke(new Rect(168, 687, 14, 2), -3f, red);
            DrawUiStroke(new Rect(314, 687, 14, 2), 2f, red);
            DrawUiStroke(new Rect(408, 687, 14, 2), -2f, red);
        }

        void DrawInkInteraction(Vector2 anchor, string action)
        {
            float pulse = 1f + Mathf.Sin(clock * 4.2f) * .04f;
            Color ink = new Color(Ink.r, Ink.g, Ink.b, .82f);
            Color red = new Color(Red.r, Red.g, Red.b, .72f);
            float x = anchor.x - 12f;
            float y = anchor.y - 12f;
            DrawUiStroke(new Rect(x - 2, y, 2.2f, 24), -4f, ink);
            DrawUiStroke(new Rect(x + 23, y + 1, 2.2f, 22), 3f, ink);
            DrawUiStroke(new Rect(x, y - 1, 23, 2.2f), 2f, ink);
            DrawUiStroke(new Rect(x + 1, y + 22, 22, 2.2f), -3f, ink);
            Text(new Rect(x, y - 1, 24, 25), "E", small, ink);
            Text(new Rect(x + 30, y, 70, 24), action, small, new Color(Ink.r, Ink.g, Ink.b, .68f));
            DrawUiStroke(new Rect(x + 34, y + 21, 34 * pulse, 1.7f), -1.5f, red);
        }

        void DrawUiStroke(Rect rect, float degrees, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * LocalRotationAround(rect.center, degrees);
            Box(rect, color);
            GUI.matrix = previous;
        }

        IEnumerator CaptureExperiencePreview()
        {
            string directory = Path.Combine(Application.dataPath, "..", "experience-preview");
            Directory.CreateDirectory(directory);
            foreach (string file in Directory.GetFiles(directory, "*.png")) File.Delete(file);

            ResetGame(false);
            bool treeColliderReached = false;
            activeAttackPower = 1;
            facing = visualFacing = attackFacing = 1;
            for (float testX = .55f; testX <= 3.4f && !treeColliderReached; testX += .05f)
            {
                X = testX;
                for (float testImpact = .38f; testImpact <= 1.2f; testImpact += .04f)
                {
                    impact = testImpact;
                    for (int tree = 0; tree < 2; tree++)
                    {
                        if (!AxeBladeOverlapsExperienceTree(tree)) continue;
                        treeColliderReached = TryStrikeExperienceTree();
                        break;
                    }
                    if (treeColliderReached) break;
                }
            }
            if (!treeColliderReached) throw new System.Exception("Visible axe blade could not reach an experience tree collider");
            ResetGame(false);
            yield return new WaitForEndOfFrame();
            SaveExperienceFrame(directory, "01-balance.png");

            X = ExperiencePlantWorldX[0];
            InteractExperience(); InteractExperience(); InteractExperience();
            if (LocalPlantCount(0) != 4 || LocalPlantCount(1) != 0 || LocalPlantCount(2) != 0)
                throw new System.Exception("Cultivation spread to an untouched site");
            X = ExperiencePlantWorldX[1];
            InteractExperience(); InteractExperience();
            X = animalWorldX;
            InteractExperience(); InteractExperience();
            for (int i = 0; i < 360; i++) UpdateExperience(1f / 60f);
            if (Ecology != EcologyOutcome.Flourishing || !ExperienceAnimalFriendly || !offspringVisible || animalPopulationVisual < 5f || !abundanceCelebrated)
                throw new System.Exception("Experience flourish path did not reach its expected result");
            if (LocalPlantCount(2) != 0) throw new System.Exception("Feeding grew plants on the other bank");
            X = 14.4f;
            yield return new WaitForEndOfFrame();
            SaveExperienceFrame(directory, "02-flourishing.png");

            ResetGame(false);
            HarmActions = 7;
            for (int i = 0; i < 240; i++) UpdateExperience(1f / 60f);
            if (Ecology != EcologyOutcome.Withering)
                throw new System.Exception("Experience withering path did not reach its expected result");
            yield return new WaitForEndOfFrame();
            SaveExperienceFrame(directory, "03-withering.png");

            File.WriteAllText(Path.Combine(directory, "result.txt"),
                "PASS\nBalance, flourishing and withering states reached.\nTree collider, tending, feeding, friendly animal, expanded plant growth, multi-animal population and abundance effect verified.\nUnity " + Application.unityVersion);
            Application.Quit(0);
        }

        void SaveExperienceFrame(string directory, string name)
        {
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, name), frame.EncodeToPNG());
            Destroy(frame);
        }
    }
}
