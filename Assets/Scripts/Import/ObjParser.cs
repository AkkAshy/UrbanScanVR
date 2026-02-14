using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace UrbanScanVR.Import
{
    /// <summary>
    /// Стриминговый парсер OBJ файлов.
    /// Читает построчно через StreamReader — не загружает весь файл в память.
    /// Поддерживает: vertices, normals, uvs, vertex colors, faces (треугольники и n-gon),
    /// material groups (usemtl), mtllib.
    /// </summary>
    public static class ObjParser
    {
        /// <summary>Результат парсинга OBJ файла</summary>
        public class ObjData
        {
            public List<Vector3> Vertices = new();
            public List<Vector3> Normals = new();
            public List<Vector2> UVs = new();
            public List<Color> VertexColors = new();    // из расширенного формата: v x y z r g b
            public List<MaterialGroup> Groups = new();
            public string MtlLibPath;                   // путь из директивы mtllib
            public bool HasVertexColors;
        }

        /// <summary>Группа граней, привязанная к одному материалу</summary>
        public class MaterialGroup
        {
            public string MaterialName;
            public List<Face> Faces = new();
        }

        /// <summary>Грань (3+ вершины)</summary>
        public class Face
        {
            public FaceVertex[] Vertices;
        }

        /// <summary>Индексы одной вершины грани</summary>
        public struct FaceVertex
        {
            public int VertexIndex;  // 1-based (как в OBJ), -1 = не указан
            public int UVIndex;
            public int NormalIndex;
        }

        // Культура для парсинга чисел (OBJ всегда использует точку как разделитель)
        static readonly CultureInfo INV = CultureInfo.InvariantCulture;

        /// <summary>
        /// Асинхронный парсинг OBJ файла.
        /// Выполняется в фоновом потоке, отчитывается о прогрессе.
        /// </summary>
        public static Task<ObjData> ParseAsync(string filePath, IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            return Task.Run(() => Parse(filePath, progress, ct), ct);
        }

        /// <summary>Синхронный парсинг (вызывается из Task.Run)</summary>
        static ObjData Parse(string filePath, IProgress<float> progress, CancellationToken ct)
        {
            var data = new ObjData();
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;
            long bytesRead = 0;
            int lastReportedPercent = -1;

            // Предварительная оценка размера списков
            // Средняя строка OBJ ~30 байт, ~40% строк — вершины
            int estimatedVertices = (int)(fileSize / 75);
            data.Vertices = new List<Vector3>(estimatedVertices);
            data.Normals = new List<Vector3>(estimatedVertices / 2);
            data.UVs = new List<Vector2>(estimatedVertices / 2);

            // Текущая группа материалов (по умолчанию — без имени)
            var currentGroup = new MaterialGroup { MaterialName = "default" };
            data.Groups.Add(currentGroup);

            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, true, 65536);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                ct.ThrowIfCancellationRequested();

                // Отслеживаем прогресс по прочитанным байтам
                bytesRead += System.Text.Encoding.UTF8.GetByteCount(line) + 1; // +1 для \n
                int percent = (int)(bytesRead * 100 / fileSize);
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report(percent / 100f);
                }

                // Пропускаем пустые строки и комментарии
                if (line.Length == 0 || line[0] == '#')
                    continue;

                // Определяем тип директивы по первым символам
                if (line.StartsWith("v "))
                    ParseVertex(line, data);
                else if (line.StartsWith("vn "))
                    ParseNormal(line, data);
                else if (line.StartsWith("vt "))
                    ParseTexCoord(line, data);
                else if (line.StartsWith("f "))
                    ParseFace(line, data, currentGroup);
                else if (line.StartsWith("usemtl "))
                {
                    // Переключаем текущую группу материалов
                    string matName = line.Substring(7).Trim();
                    currentGroup = new MaterialGroup { MaterialName = matName };
                    data.Groups.Add(currentGroup);
                }
                else if (line.StartsWith("mtllib "))
                {
                    data.MtlLibPath = line.Substring(7).Trim();
                }
            }

            // Удаляем пустые группы
            data.Groups.RemoveAll(g => g.Faces.Count == 0);

            progress?.Report(1f);
            return data;
        }

        /// <summary>Парсинг вершины: v x y z [r g b]</summary>
        static void ParseVertex(string line, ObjData data)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 4)
            {
                float x = float.Parse(parts[1], INV);
                float y = float.Parse(parts[2], INV);
                float z = float.Parse(parts[3], INV);
                data.Vertices.Add(new Vector3(x, y, z));

                // Vertex colors — расширенный формат OBJ (v x y z r g b)
                if (parts.Length >= 7)
                {
                    float r = float.Parse(parts[4], INV);
                    float g = float.Parse(parts[5], INV);
                    float b = float.Parse(parts[6], INV);
                    data.VertexColors.Add(new Color(r, g, b, 1f));
                    data.HasVertexColors = true;
                }
            }
        }

        /// <summary>Парсинг нормали: vn x y z</summary>
        static void ParseNormal(string line, ObjData data)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                float x = float.Parse(parts[1], INV);
                float y = float.Parse(parts[2], INV);
                float z = float.Parse(parts[3], INV);
                data.Normals.Add(new Vector3(x, y, z));
            }
        }

        /// <summary>Парсинг текстурных координат: vt u v</summary>
        static void ParseTexCoord(string line, ObjData data)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                float u = float.Parse(parts[1], INV);
                float v = float.Parse(parts[2], INV);
                data.UVs.Add(new Vector2(u, v));
            }
        }

        /// <summary>
        /// Парсинг грани: f v1 v2 v3 ... (n-gon)
        /// Форматы: f v, f v/vt, f v/vt/vn, f v//vn
        /// Поддерживает отрицательные индексы (относительные)
        /// </summary>
        static void ParseFace(string line, ObjData data, MaterialGroup group)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return; // минимум 3 вершины + "f"

            int vertexCount = parts.Length - 1;
            var faceVertices = new FaceVertex[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                faceVertices[i] = ParseFaceVertex(parts[i + 1], data);
            }

            // Триангуляция n-gon → веером (fan triangulation)
            // Для треугольника: одна грань
            // Для квада/n-gon: (n-2) треугольника
            for (int i = 1; i < vertexCount - 1; i++)
            {
                var face = new Face
                {
                    Vertices = new[] { faceVertices[0], faceVertices[i], faceVertices[i + 1] }
                };
                group.Faces.Add(face);
            }
        }

        /// <summary>Парсинг одной вершины грани (v/vt/vn)</summary>
        static FaceVertex ParseFaceVertex(string token, ObjData data)
        {
            var fv = new FaceVertex { VertexIndex = -1, UVIndex = -1, NormalIndex = -1 };
            var parts = token.Split('/');

            // Vertex index (обязательный)
            if (parts.Length >= 1 && parts[0].Length > 0)
            {
                int idx = int.Parse(parts[0], INV);
                fv.VertexIndex = idx > 0 ? idx - 1 : data.Vertices.Count + idx; // отрицательный = относительный
            }

            // UV index (опциональный)
            if (parts.Length >= 2 && parts[1].Length > 0)
            {
                int idx = int.Parse(parts[1], INV);
                fv.UVIndex = idx > 0 ? idx - 1 : data.UVs.Count + idx;
            }

            // Normal index (опциональный)
            if (parts.Length >= 3 && parts[2].Length > 0)
            {
                int idx = int.Parse(parts[2], INV);
                fv.NormalIndex = idx > 0 ? idx - 1 : data.Normals.Count + idx;
            }

            return fv;
        }

        /// <summary>
        /// Собирает Unity Mesh из распарсенных данных OBJ.
        /// Вызывать ТОЛЬКО из главного потока (Unity API)!
        /// </summary>
        public static GameObject BuildGameObject(ObjData data, Dictionary<string, Material> materials = null)
        {
            var root = new GameObject("ImportedModel");

            // Если нет групп — создаём один меш из всех данных
            if (data.Groups.Count == 0)
                return root;

            foreach (var group in data.Groups)
            {
                var childGO = new GameObject(group.MaterialName);
                childGO.transform.SetParent(root.transform, false);

                var mesh = BuildMesh(data, group);
                if (mesh == null) continue;

                var meshFilter = childGO.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;

                var meshRenderer = childGO.AddComponent<MeshRenderer>();

                // Назначаем материал
                if (materials != null && materials.TryGetValue(group.MaterialName, out var mat))
                {
                    meshRenderer.material = mat;
                }
                else if (data.HasVertexColors)
                {
                    // Vertex colors — используем шейдер с поддержкой вертексных цветов
                    var vcMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    vcMat.SetFloat("_Surface", 0); // Opaque
                    meshRenderer.material = vcMat;
                }
                else
                {
                    // Дефолтный серый материал
                    var defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    defaultMat.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f));
                    meshRenderer.material = defaultMat;
                }
            }

            return root;
        }

        /// <summary>Строит Unity Mesh из одной группы материалов</summary>
        static Mesh BuildMesh(ObjData data, MaterialGroup group)
        {
            if (group.Faces.Count == 0) return null;

            // OBJ использует индексированную геометрию (общие вершины),
            // Unity требует per-face-vertex данные.
            // Разворачиваем индексы в плоские массивы.
            int totalVerts = group.Faces.Count * 3; // каждая грань = 3 вершины (уже триангулировано)

            var vertices = new Vector3[totalVerts];
            var normals = new Vector3[totalVerts];
            var uvs = new Vector2[totalVerts];
            var colors = data.HasVertexColors ? new Color[totalVerts] : null;
            var indices = new int[totalVerts];

            bool hasNormals = data.Normals.Count > 0;
            bool hasUVs = data.UVs.Count > 0;

            int vi = 0;
            foreach (var face in group.Faces)
            {
                for (int i = 0; i < 3; i++)
                {
                    var fv = face.Vertices[i];

                    // Позиция вершины
                    if (fv.VertexIndex >= 0 && fv.VertexIndex < data.Vertices.Count)
                        vertices[vi] = data.Vertices[fv.VertexIndex];

                    // Нормаль
                    if (hasNormals && fv.NormalIndex >= 0 && fv.NormalIndex < data.Normals.Count)
                        normals[vi] = data.Normals[fv.NormalIndex];

                    // UV
                    if (hasUVs && fv.UVIndex >= 0 && fv.UVIndex < data.UVs.Count)
                        uvs[vi] = data.UVs[fv.UVIndex];

                    // Vertex colors
                    if (colors != null && fv.VertexIndex >= 0 && fv.VertexIndex < data.VertexColors.Count)
                        colors[vi] = data.VertexColors[fv.VertexIndex];

                    indices[vi] = vi;
                    vi++;
                }
            }

            // Создаём Unity Mesh
            var mesh = new Mesh();
            mesh.name = group.MaterialName;

            // UInt32 индексы — поддержка > 65535 вершин
            mesh.indexFormat = totalVerts > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.vertices = vertices;
            mesh.triangles = indices;

            if (hasNormals)
                mesh.normals = normals;
            else
                mesh.RecalculateNormals(); // генерируем нормали если в OBJ их нет

            if (hasUVs)
                mesh.uv = uvs;

            if (colors != null)
                mesh.colors = colors;

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
