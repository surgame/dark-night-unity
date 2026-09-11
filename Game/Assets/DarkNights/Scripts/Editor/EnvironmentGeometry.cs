using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 首版环境的有限几何转换，保存像素矩形、月亮圆片和固定地表网格以供原生编辑。
    /// 输入公式来自冻结源，不使用玩法随机数；所有新资源只写入调用者已预检的空目录。
    /// </summary>
    public static class EnvironmentGeometry
    {
        public static Sprite White(string root)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "White", filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            AssetDatabase.CreateAsset(texture, root + "/White.asset");
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0, 1), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "White"; AssetDatabase.AddObjectToAsset(sprite, texture); return sprite;
        }

        public static Sprite Circle(string root)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Circle", filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Vector2(x + 0.5f - size / 2, y + 0.5f - size / 2).sqrMagnitude <= size * size / 4 ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, root + "/Circle.asset");
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Circle"; AssetDatabase.AddObjectToAsset(sprite, texture); return sprite;
        }

        public static Mesh Terrain(string root)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            for (int i = 0; i < 190; i++)
            {
                float x = (float)(i * 79.37 % 1100), y = 14 + (float)(i * 13.81 % 115);
                Color color = i % 2 == 0 ? new Color32(61, 58, 48, 255) : new Color32(41, 40, 32, 255);
                int index = vertices.Count;
                vertices.Add(new Vector3(x / 100, -y / 100));
                vertices.Add(new Vector3((x + 2 + i % 4) / 100, -y / 100));
                vertices.Add(new Vector3((x + 2 + i % 4) / 100, -(y + 1 + i % 2) / 100));
                vertices.Add(new Vector3(x / 100, -(y + 1 + i % 2) / 100));
                for (int n = 0; n < 4; n++) colors.Add(color);
                triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
            }
            var mesh = new Mesh { name = "Terrain Details", vertices = vertices.ToArray(), colors = colors.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, root + "/Terrain.asset"); return mesh;
        }

        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector2 at, Vector2 scale, int order, Material material)
        {
            var child = Child(parent, name); child.localPosition = at; child.localScale = new Vector3(scale.x, scale.y, 1);
            var renderer = child.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = order; renderer.sharedMaterial = material; return renderer;
        }
        public static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        public static void Array(SerializedObject data, string key, UnityEngine.Object[] values)
        {
            var property = data.FindProperty(key); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
