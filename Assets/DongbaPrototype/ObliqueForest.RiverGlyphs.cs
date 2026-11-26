using UnityEngine;

namespace DongbaPrototype
{
    public partial class ObliqueForest
    {
        // Game-art adaptations of the supplied dictionary silhouettes.
        static Sprite RiverGlyph(params Vector2[][] strokes)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            foreach (var path in strokes)
            {
                for (int i = 1; i < path.Length; i++)
                {
                    Vector2 a = path[i - 1], delta = path[i] - a;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude * 2));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 point = a + delta * (s / (float)steps);
                        int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
                        for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            if (dx * dx + dy * dy <= 5 && x + dx >= 0 && x + dx < size && y + dy >= 0 && y + dy < size)
                                pixels[x + dx + size * (y + dy)] = new Color32(63, 39, 26, 255);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .06f), 100);
        }

        static Vector2[] InkStroke(params float[] coordinates)
        {
            var path = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < path.Length; i++)
                path[i] = new Vector2(coordinates[2 * i], coordinates[2 * i + 1]);
            return path;
        }

        static Sprite DrawRiverDeer() => RiverGlyph(
            InkStroke(17,53, 25,64, 49,66, 68,62, 77,70, 89,91, 113,88, 108,81, 93,78, 82,51, 66,42, 35,43, 17,53),
            InkStroke(19,56, 9,64, 12,53, 19,52),
            InkStroke(31,44, 30,26, 22,10, 29,10), InkStroke(39,43, 45,28, 39,9),
            InkStroke(71,44, 76,25, 76,8, 83,8), InkStroke(79,49, 88,27, 93,10),
            InkStroke(88,91, 73,99, 65,114, 62,122), InkStroke(73,99, 56,101, 46,115),
            InkStroke(94,90, 89,105, 82,121), InkStroke(89,105, 101,115, 104,124));

        static Sprite DrawWoodBranch() => RiverGlyph(
            InkStroke(16,20, 38,42, 62,56, 93,68, 113,74),
            InkStroke(48,49, 70,49, 90,43),
            InkStroke(67,58, 79,74, 96,83),
            InkStroke(85,65, 106,63, 118,59));

        static Sprite DrawAntler() => RiverGlyph(
            InkStroke(42,18, 65,40, 83,69, 91,98, 90,121),
            InkStroke(67,44, 45,57, 26,86, 14,114),
            InkStroke(45,57, 27,59, 12,74),
            InkStroke(84,72, 106,84, 116,106),
            InkStroke(91,99, 75,114, 74,124));

        static Sprite DrawRiverChicken() => RiverGlyph(
            InkStroke(25,45, 37,61, 62,62, 75,70, 79,90, 92,98, 109,91, 120,84, 104,82, 98,61, 85,44, 60,33, 39,37, 25,45),
            InkStroke(78,90, 77,104, 84,99, 88,111, 93,99, 100,105, 102,96),
            InkStroke(31,43, 19,52, 8,48), InkStroke(40,44, 22,63, 6,62),
            InkStroke(55,34, 56,17, 42,11, 60,14), InkStroke(76,39, 79,21, 93,15),
            InkStroke(99,90, 101,90), InkStroke(48,49, 65,45, 81,57));

        static Sprite DrawFeather() => RiverGlyph(
            InkStroke(14,25, 44,47, 76,68, 113,98),
            InkStroke(37,44, 44,69, 66,93, 113,98, 105,76, 82,60, 57,52),
            InkStroke(53,56, 60,77), InkStroke(69,64, 81,85), InkStroke(82,74, 98,89));

        static Sprite DrawSpear() => RiverGlyph(
            InkStroke(38,14, 89,111),
            InkStroke(81,94, 82,110, 100,125, 99,105, 88,93, 81,94));

        static Sprite DrawRiverStone() => RiverGlyph(InkStroke(20,28, 34,61, 84,71, 110,43, 96,21, 20,28));
    }
}
