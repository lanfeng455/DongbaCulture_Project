using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        enum RiverTask { Axe, Trees, Feather, Spear, Boar, Return, Done }
        RiverTask riverTask;
        int extraFelled, selectedSlot, carriedStone, hotbarDirection;
        Transform riverStone;
        bool spearOwned;
        Sprite riverAxeImage, riverSpearImage;
        UnityEngine.UI.Text riverObjective;
        readonly UnityEngine.UI.Text[] riverHearts = new UnityEngine.UI.Text[5];
        readonly UnityEngine.UI.Text[] riverSlots = new UnityEngine.UI.Text[6];
        readonly UnityEngine.UI.Image[] riverSlotIcons = new UnityEngine.UI.Image[6];
        Sprite[] riverIcons;
        float taskTickTime, heartShakeUntil;
        int lastRiverHealth = -1, shakingHeart = -1;
        string completedRiverTask;
        AudioSource riverBirds;
        AudioClip riverBirdNote;
        float nextRiverBird;
        readonly System.Collections.Generic.HashSet<Being> wiltedRiverPlants = new();
        bool HasCuttingTool => woodenAxe && (selectedSlot == 0 || selectedSlot == 1 && spearOwned);
        bool CanCraftSpear => riverTask == RiverTask.Spear && !spearOwned && carriedWood >= 2 && carriedStone > 0 && attackAge < 0;

        readonly System.Collections.Generic.Dictionary<Being, float> grassCoverShake = new();

        float RiverGrassAngle(Being grass, float idleAngle)
        {
            if (!grassCoverShake.TryGetValue(grass, out float began)) return idleAngle;
            float progress = (clock - began) / .42f;
            if (progress >= 1) { grassCoverShake.Remove(grass); return idleAngle; }
            return idleAngle + Mathf.Sin(progress * Mathf.PI * 4) * (1 - progress) * 16;
        }

        void AdvanceRiverTask(RiverTask next)
        {
            completedRiverTask = RiverTaskLabel();
            taskTickTime = Time.unscaledTime + .9f;
            riverTask = next;
        }

        void DrawRiverHud(Transform panel)
        {
            if (!FirstAxeActive) return;
            if (riverObjective == null)
            {
                for (int i = 0; i < riverHearts.Length; i++)
                    riverHearts[i] = UILabel("River heart " + i, panel, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(35 + 53 * i, -32), new Vector2(50, 58), 42, "");
                riverObjective = UILabel("River task", panel, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(35, -104), new Vector2(820, 61), 31, "");
                riverIcons = new[] { Get("axe_glyph"), DrawSpear(), DrawWoodBranch(), DrawRiverStone(), DrawFeather(), DrawAntler() };
                for (int i = 0; i < riverSlots.Length; i++)
                {
                    var slot = UIBlock("Hotbar slot " + i, panel, new Vector2(.5f, 0), new Vector2(.5f, 0),
                        new Vector2((i - 2.5f) * 139, 12), new Vector2(128, 88));
                    QuietPaper(slot);
                    var icon = UIBlock("Item icon", slot, Vector2.one * .5f, Vector2.one * .5f,
                        new Vector2(0, 15), new Vector2(51, 47));
                    riverSlotIcons[i] = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
                    riverSlotIcons[i].sprite = riverIcons[i];
                    riverSlotIcons[i].preserveAspect = true;
                    riverSlots[i] = UILabel("Item", slot, Vector2.one * .5f, Vector2.one * .5f,
                        new Vector2(0, -24), new Vector2(121, 33), 24, "");
                    riverSlots[i].alignment = TextAnchor.MiddleCenter;
                }
            }
            foreach (string element in new[] { "Compact identity and vitality", "Story objective paper" })
            {
                var old = panel.Find(element);
                if (old != null) old.gameObject.SetActive(false);
            }
            if (lastRiverHealth >= 0 && health < lastRiverHealth)
            {
                heartShakeUntil = Time.unscaledTime + .25f;
                shakingHeart = Mathf.Clamp(health, 0, 4);
            }
            lastRiverHealth = health;
            int filled = Mathf.Clamp(health, 0, 5);
            for (int i = 0; i < riverHearts.Length; i++)
            {
                bool shaking = i == shakingHeart && Time.unscaledTime < heartShakeUntil;
                riverHearts[i].text = i < filled || shaking ? "♥" : "♡";
                riverHearts[i].color = new Color(.66f, .26f, .21f);
                riverHearts[i].rectTransform.anchoredPosition =
                    new Vector2(35 + 53 * i + (shaking ? Mathf.Sin(Time.unscaledTime * 95) * 5 : 0), -32);
            }
            riverObjective.text = Time.unscaledTime < taskTickTime ? "✓ " + completedRiverTask : RiverTaskLabel();
            riverSlots[0].text = woodenAxe ? "Axe" : "—";
            riverSlots[1].text = spearOwned ? "Spear" : "—";
            riverSlots[2].text = $"Wood {carriedWood}";
            riverSlots[3].text = $"Stone {carriedStone}";
            riverSlots[4].text = $"Feather {carriedFeather}";
            riverSlots[5].text = $"Antler {carriedAntler}";
            for (int i = 0; i < riverSlots.Length; i++)
            {
                riverSlots[i].fontStyle = i == selectedSlot ? FontStyle.Bold : FontStyle.Normal;
                riverSlots[i].color = i == selectedSlot ? new Color(.55f, .2f, .12f) : uiInk;
                riverSlotIcons[i].enabled = i > 1 || (i == 0 ? woodenAxe : spearOwned);
                riverSlots[i].transform.parent.GetComponent<UnityEngine.UI.Image>().color =
                    i == selectedSlot ? new Color(.97f, .83f, .56f) : new Color(.96f, .925f, .85f, .95f);
            }
        }

        void RiverTreeFelled(Being tree)
        {
            Wash(tree.home, 1.15f, new Color(.052f, .038f, .03f, .85f));
            int wilted = 0;
            foreach (var plant in plants)
            {
                if (plant.tree || plant.targetScale < .5f || Vector3.Distance(plant.home, tree.home) > 2.5f) continue;
                plant.targetScale = .38f;
                wiltedRiverPlants.Add(plant);
                if (++wilted >= 3) break;
            }
            if (riverTask == RiverTask.Trees && ++extraFelled >= 2) AdvanceRiverTask(RiverTask.Feather);
        }

        void RiverFeatherCollected()
        {
            if (riverTask != RiverTask.Feather && riverTask != RiverTask.Trees) return;
            if (riverTask == RiverTask.Trees) return;
            ShowRiverFeatherDialogue();
        }

        void ShowRiverFeatherDialogue()
        {
            if (riverTask != RiverTask.Feather || carriedFeather == 0 || ManualStoryDialogueActive) return;
            AdvanceRiverTask(RiverTask.Spear);
            BeginManualStoryDialogue(new[] { "Son: What about the meat?",
                "Father: We have enough to eat. The feathers will sell.",
                "Son: Then why did we kill it?", "Father: Just do it.", "Son: Fine..." });
            riverStone = Billboard("River stone", null,
                DrawRiverStone(), .36f);
            riverStone.position = new Vector3(-1, 0, -1.8f);
            looseWood.Add(riverStone);
        }

        bool TryCraftSpear()
        {
            if (!CanCraftSpear) return false;
            carriedWood -= 2;
            carriedStone--;
            spearOwned = true;
            riverAxeImage = axe.GetComponent<SpriteRenderer>().sprite;
            var spearGlyph = DrawSpear();
            riverSpearImage = Sprite.Create(spearGlyph.texture, spearGlyph.rect, new Vector2(.45f, .41f), 100);
            selectedSlot = 1;
            AdvanceRiverTask(RiverTask.Boar);
            Tell("Spear crafted.");
            return true;
        }

        void UpdateRiverHotbar(float horizontal)
        {
            if (Mathf.Abs(horizontal) < .25f) hotbarDirection = 0;
            if (!FirstAxeActive || !XRExperienceReady || attackAge >= 0 || Mathf.Abs(horizontal) < .65f || hotbarDirection != 0) return;
            hotbarDirection = horizontal > 0 ? 1 : -1;
            selectedSlot = (selectedSlot + hotbarDirection + 6) % 6;
        }

        void UpdateRiverOpening()
        {
            if (!FirstAxeActive || !XRExperienceReady || ManualStoryDialogueActive) return;
            UpdateRiverBirds();
            if (spearOwned)
                axe.GetComponent<SpriteRenderer>().sprite = selectedSlot == 1 ? riverSpearImage : riverAxeImage;
            ShowRiverFeatherDialogue();
            if (riverTask != RiverTask.Return || Vector3.Distance(Feet, ForestSpawn) > 6f) return;
            AdvanceRiverTask(RiverTask.Done);
            knifeDropPoint = Feet + Vector3.left * 2;
            EnsureStoryShu();
            BeginManualStoryDialogue(new[] { "Shu: Look at what you have left behind.", "Son: Father!" }, () =>
            {
                storyFatherRoot.gameObject.SetActive(false);
                storyShu.gameObject.SetActive(false);
                storyOpeningState = StoryOpeningState.Captured;
                FatherCaptured?.Invoke();
                Tell("Shu took Father.");
            });
        }

        void UpdateRiverBirds()
        {
            if (riverBirds == null)
            {
                riverBirds = gameObject.AddComponent<AudioSource>();
                riverBirds.playOnAwake = false;
                riverBirdNote = MakeRiverCall("Short birdsong", 1750, .22f);
                nextRiverBird = clock + 1;
            }
            if (clock < nextRiverBird) return;
            nextRiverBird = clock + 3 + extraFelled * 3.5f;
            if (Vector3.Distance(Feet, ForestSpawn) < 19)
                riverBirds.PlayOneShot(riverBirdNote, Mathf.Max(.08f, .38f - extraFelled * .12f));
        }

        static AudioClip MakeRiverCall(string label, float pitch, float duration)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(duration * rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * i / samples.Length);
                samples[i] = Mathf.Sin(2 * Mathf.PI * (pitch + 520 * time) * time) * envelope * .35f;
            }
            var clip = AudioClip.Create(label, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        string RiverTaskLabel()
        {
            switch (riverTask)
            {
                case RiverTask.Axe: return "Craft a Wooden Axe";
                case RiverTask.Trees: return $"Cut 2 More Trees · {extraFelled}/2";
                case RiverTask.Feather: return "Collect a Feather · 0/1";
                case RiverTask.Spear: return "Craft a Spear";
                case RiverTask.Boar: return "Drive Off the Boar";
                case RiverTask.Return: return "Return to the riverbank";
                default: return "Find Father";
            }
        }
    }
}
