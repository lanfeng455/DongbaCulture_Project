using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        // The original symbol supplies the head and trunk. Limbs are game-art animation.
        readonly Color rigInk = new Color(.18f, .12f, .076f);
        Sprite sideSonHead, forwardSonHead, rearSonHead;
        Vector3 sideSonHeadPosition;
        LineRenderer sonTorsoJoin;

        void BuildFirstAxeRig()
        {
            firstAxeUpper = new GameObject("Son glyph torso and head").transform;
            firstAxeUpper.SetParent(body, false);
            firstAxeHead = CropSonGlyph("Original glyph head", 8, 8, 82, 54);
            firstAxeChest = CropSonGlyph("Original glyph torso", 59, 54, 72, 72);
            sideSonHead = firstAxeHead.sprite;
            sideSonHeadPosition = firstAxeHead.transform.localPosition;
            forwardSonHead = RiverGlyph(InkStroke(37,42, 49,63, 80,63, 91,46, 76,20, 50,20, 37,42),
                InkStroke(51,48, 54,48), InkStroke(76,48, 79,48),
                InkStroke(52,61, 27,80, 12,106, 47,89, 64,64));
            rearSonHead = RiverGlyph(InkStroke(37,42, 47,62, 69,70, 88,57, 90,36, 77,19, 52,19, 37,42),
                InkStroke(59,66, 22,84, 11,112, 48,93, 68,70), InkStroke(48,25, 64,34, 80,25));
            firstAxeInk = new Material(Shader.Find("Sprites/Default"));
            sonTorsoJoin = CreateSonLimb("Pelvis to chest");
            firstAxeArm = CreateSonLimb("Near arm");
            farArm = CreateSonLimb("Far arm");
            leftLeg = CreateSonLimb("Left leg");
            rightLeg = CreateSonLimb("Right leg");

            var fistTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color[256];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float radius = new Vector2(x - 7.5f, y - 7.5f).magnitude;
                pixels[x + y * 16] = new Color(rigInk.r, rigInk.g, rigInk.b, Mathf.Clamp01(7.5f - radius));
            }
            fistTexture.SetPixels(pixels);
            fistTexture.Apply(false, true);
            var fist = new GameObject("Near hand");
            fist.transform.SetParent(body, false);
            firstAxeFist = fist.AddComponent<SpriteRenderer>();
            firstAxeFist.sprite = Sprite.Create(fistTexture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 200);
        }

        Vector3 SonPoint(float x, float y)
        {
            Sprite source = playerImage.sprite;
            return new Vector3(x - source.pivot.x, source.rect.height - y - source.pivot.y, 0) / source.pixelsPerUnit;
        }

        SpriteRenderer CropSonGlyph(string label, int left, int top, int right, int bottom)
        {
            var original = playerImage.sprite;
            var crop = new GameObject(label);
            crop.transform.SetParent(firstAxeUpper, false);
            crop.transform.localPosition = SonPoint((left + right) * .5f, (top + bottom) * .5f);
            var image = crop.AddComponent<SpriteRenderer>();
            image.sprite = Sprite.Create(original.texture,
                new Rect(left, original.rect.height - bottom, right - left, bottom - top),
                Vector2.one * .5f, original.pixelsPerUnit);
            return image;
        }

        LineRenderer CreateSonLimb(string label)
        {
            var limb = new GameObject(label);
            limb.transform.SetParent(body, false);
            var line = limb.AddComponent<LineRenderer>();
            line.sharedMaterial = firstAxeInk;
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            return line;
        }

        void DrawSonLimb(LineRenderer limb, Vector3 shoulder, Vector3 middle, Vector3 tip, float width, int order, Color color)
        {
            limb.startWidth = limb.endWidth = width * body.lossyScale.y;
            limb.startColor = limb.endColor = color;
            limb.sortingOrder = order;
            limb.SetPosition(0, shoulder);
            limb.SetPosition(1, middle);
            limb.SetPosition(2, tip);
        }

        void UpdateFirstAxeRig(float hop)
        {
            if (firstAxeArm == null) return;
            Color tint = damageCooldown > 0 ? new Color(1, .55f, .45f) : Color.white;
            playerImage.color = Color.clear;
            body.localPosition = Vector3.up * hop;
            int order = playerImage.sortingOrder;
            bool knocking = attackAge >= 0 && !HasCuttingTool;
            float gait = gaitDistance / Mathf.Max(.1f, FootfallDistance) * Mathf.PI;
            float blend = attackAge < 0 ? walkBlend : 0;
            float phase = Mathf.Sin(gait) * blend;
            float stride = Vector3.Dot(gaitDirection, FirstAxeCardRight()) * phase;
            float torsoSway = Mathf.Cos(gait) * WalkSway * blend;
            Vector3 center = SonPoint(66, 59);
            Vector3 towardViewer = Vector3.ProjectOnPlane(cam.transform.position - Feet, Vector3.up).normalized;
            float depth = Vector3.Dot(gaitDirection, towardViewer);
            bool depthView = attackAge < 0 && Mathf.Abs(depth) > .72f;
            firstAxeHead.sprite = depthView ? (depth > 0 ? forwardSonHead : rearSonHead) : sideSonHead;
            firstAxeHead.transform.localPosition = depthView ? SonPoint(64, 51) : sideSonHeadPosition;
            firstAxeHead.transform.localScale = depthView ? Vector3.one * .43f : Vector3.one;
            int artFacing = depthView ? 1 : facing;
            firstAxeUpper.localPosition = new Vector3((1 - artFacing) * center.x + torsoSway, 0, 0);
            firstAxeUpper.localScale = new Vector3(artFacing, 1, 1);
            firstAxeHead.sortingOrder = firstAxeChest.sortingOrder = order + 1;
            firstAxeHead.color = firstAxeChest.color = tint;

            Color ink = rigInk * tint;
            Vector3 hip = SonPoint(65, 69);
            Vector3 leftFoot = SonPoint(56, 101) + new Vector3(stride * .13f, Mathf.Max(phase, 0) * WalkLift, 0);
            Vector3 rightFoot = SonPoint(81, 101) - new Vector3(stride * .13f, Mathf.Min(phase, 0) * WalkLift, 0);
            int leftOrder = depthView && phase * depth > 0 ? order - 1 : order;
            DrawSonLimb(leftLeg, hip, Vector3.Lerp(hip, leftFoot, .52f), leftFoot, .055f, leftOrder, ink);
            DrawSonLimb(rightLeg, hip, Vector3.Lerp(hip, rightFoot, .52f), rightFoot, .055f, 2 * order - 1 - leftOrder, ink);

            Vector3 shoulder = center + Vector3.right * (facing * .025f + torsoSway);
            Vector3 chest = center + Vector3.right * torsoSway;
            Vector3 farTip = shoulder + new Vector3(-facing * .20f + stride * .1f, -.077f, 0);
            DrawSonLimb(farArm, shoulder, (shoulder + farTip) * .5f, farTip, .032f, order, ink);

            Vector3 idleTip = shoulder + new Vector3(facing * .24f - stride * .1f, -.062f, 0);
            Vector3 nearTip = idleTip;
            if (knocking)
            {
                float contact = Mathf.Clamp(BareHandContact, .14f, Mathf.Max(.3f, BareHandSeconds) - .04f);
                Vector3 target = body.InverseTransformPoint(firstAxeStrikePoint);
                Vector3 load = new Vector3(-facing * .08f, -.06f, 0);
                Vector3 extension = target - center;
                Vector3 chestOffset = attackAge < .12f
                    ? Vector3.Lerp(Vector3.zero, load, attackAge / .12f)
                    : attackAge <= contact
                        ? Vector3.Lerp(load, extension, (attackAge - .12f) / (contact - .12f))
                        : Vector3.Lerp(extension, Vector3.zero, (attackAge - contact) / Mathf.Max(.04f, BareHandSeconds - contact));
                firstAxeUpper.localPosition += chestOffset;
                shoulder += chestOffset;
                chest += chestOffset;
                nearTip = shoulder + new Vector3(-facing * .08f, -.12f, 0);
                farTip = shoulder + new Vector3(-facing * .15f, -.07f, .005f);
                DrawSonLimb(farArm, shoulder, (shoulder + farTip) * .5f, farTip, .032f, order, ink);
                firstAxeArmTip = body.TransformPoint(center + chestOffset);
            }
            DrawSonLimb(sonTorsoJoin, hip, Vector3.Lerp(hip, chest, .55f), chest, .061f, order, ink);
            Vector3 elbow = Vector3.Lerp(shoulder, nearTip, .48f) + Vector3.down * (knocking ? .055f : .025f);
            DrawSonLimb(firstAxeArm, shoulder, elbow, nearTip, .04f, order + 2, ink);
            firstAxeFist.enabled = !HasCuttingTool && !knocking;
            firstAxeFist.transform.localPosition = nearTip;
            firstAxeFist.sortingOrder = order + 3;
            firstAxeFist.color = tint;
            hand.localPosition = nearTip;
            axe.GetComponent<SpriteRenderer>().sortingOrder = order + 3;
        }
    }
}
