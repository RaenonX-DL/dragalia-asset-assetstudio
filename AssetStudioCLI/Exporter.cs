using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AssetStudio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TGASharpLib;

namespace AssetStudioCLI
{
    internal static class Exporter
    {
        private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath)
        {
            return TryExportFile(dir, item, extension, out fullPath, true);
        }

        private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath,
            bool useSuffix)
        {
            var fileName = FixFileName(item.Text);

            if (useSuffix && !string.IsNullOrEmpty(Studio.exportSuffix))
            {
                fileName += Studio.exportSuffix;
            }

            fullPath = Path.Combine(dir, fileName + extension);
            Directory.CreateDirectory(dir);

            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }

            if (Studio.skipExists)
                return false;

            if (Properties.Settings.Default.convertReplaceExists)
                return true;

            fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }

            return false;
        }

        public static bool ExportTexture2D(AssetItem item, string exportPath)
        {
            var m_Texture2D = (Texture2D) item.Asset;
            if (Properties.Settings.Default.convertTexture)
            {
                var bitmap = m_Texture2D.ConvertToBitmap(true);
                if (bitmap == null)
                    return false;
                ImageFormat format = null;
                var ext = Properties.Settings.Default.convertType;
                var tga = false;
                switch (ext)
                {
                    case "BMP":
                        format = ImageFormat.Bmp;
                        break;
                    case "PNG":
                        format = ImageFormat.Png;
                        break;
                    case "JPEG":
                        format = ImageFormat.Jpeg;
                        break;
                    case "TGA":
                        tga = true;
                        break;
                }

                if (!TryExportFile(exportPath, item, "." + ext.ToLower(), out var exportFullPath))
                    return false;
                if (tga)
                {
                    var file = new TGA(bitmap);
                    file.Save(exportFullPath);
                }
                else
                {
                    try
                    {
                        bitmap.Save(exportFullPath, format);
                        bitmap.Dispose();
                    }
                    catch (ExternalException)
                    {
                        return ExportTexture2D(item, exportPath);
                    }
                }

                return true;
            }
            else
            {
                if (!TryExportFile(exportPath, item, ".tex", out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_Texture2D.image_data.GetData());
                return true;
            }
        }

        public static bool ExportAudioClip(AssetItem item, string exportPath)
        {
            var m_AudioClip = (AudioClip) item.Asset;
            var m_AudioData = m_AudioClip.m_AudioData.GetData();
            if (m_AudioData == null || m_AudioData.Length == 0)
                return false;
            var converter = new AudioClipConverter(m_AudioClip);
            if (Properties.Settings.Default.convertAudio && converter.IsSupport)
            {
                if (!TryExportFile(exportPath, item, ".wav", out var exportFullPath))
                    return false;
                var buffer = converter.ConvertToWav();
                if (buffer == null)
                    return false;
                File.WriteAllBytes(exportFullPath, buffer);
            }
            else
            {
                if (!TryExportFile(exportPath, item, converter.GetExtensionName(), out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_AudioData);
            }

            return true;
        }

        public static bool ExportShader(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".shader", out var exportFullPath))
                return false;
            var m_Shader = (Shader) item.Asset;
            var str = m_Shader.Convert();
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public static bool ExportTextAsset(AssetItem item, string exportPath)
        {
            var m_TextAsset = (TextAsset) (item.Asset);
            var extension = ".txt";
            if (Properties.Settings.Default.restoreExtensionName)
            {
                if (!string.IsNullOrEmpty(item.Container))
                {
                    extension = Path.GetExtension(item.Container);
                }
            }

            if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_TextAsset.m_Script);
            return true;
        }

        public static bool ExportMonoBehaviour(AssetItem item, string exportPath)
        {
            var useSuffix = true;
            if (!string.IsNullOrEmpty(item.Container))
            {
                var ext = Path.GetExtension(item.Container).ToLower();
                if (ext == ".prefab")
                {
                    useSuffix = false;
                    exportPath = !string.IsNullOrEmpty(Studio.exportSuffix)
                        ? Path.Combine(exportPath,
                            Path.GetFileNameWithoutExtension(item.Container) + Studio.exportSuffix + ext)
                        : Path.Combine(exportPath, Path.GetFileName(item.Container));
                }
            }

            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath, useSuffix: useSuffix))
                return false;
            var m_MonoBehaviour = (MonoBehaviour) item.Asset;
            var type = m_MonoBehaviour.ToType();
            if (type == null)
            {
                var nodes = Studio.MonoBehaviourToTypeTreeNodes(m_MonoBehaviour);
                type = m_MonoBehaviour.ToType(nodes);
            }

            var str = JsonConvert.SerializeObject(type, Formatting.Indented);
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public static bool ExportConvertGameObject(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".prefab.json", out var exportFullPath))
                return false;

            var m_GameObject = (GameObject) item.Asset;
            var nodes = new JObject
            {
                {"Name", m_GameObject.m_Name}
            };
            var components = new JArray();
            nodes.Add("Components", components);
            foreach (var pptr in m_GameObject.m_Components)
            {
                if (!pptr.TryGet(out var c))
                {
                    continue;
                }

                var type = c.ToType();
                if (type == null)
                {
                    continue;
                }

                switch (c)
                {
                    case MonoBehaviour b:
                        if (b.m_Name == "" && b.m_Script.TryGet(out var m_Script))
                        {
                            type.Insert(0, "$Script", m_Script.m_ClassName);
                        }
                        else
                        {
                            type.Insert(0, "$Name", b.m_Name);
                        }

                        break;
                    default:
                        type.Insert(0, "$Type", c.type.ToString());
                        break;
                }

                components.Add(JObject.FromObject(type));
            }

            var str = nodes.ToString(Formatting.Indented);
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public static bool ExportFont(AssetItem item, string exportPath)
        {
            var m_Font = (Font) item.Asset;
            if (m_Font.m_FontData == null) return false;

            var extension = ".ttf";
            if (m_Font.m_FontData[0] == 79 && m_Font.m_FontData[1] == 84 && m_Font.m_FontData[2] == 84 &&
                m_Font.m_FontData[3] == 79)
            {
                extension = ".otf";
            }

            if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_Font.m_FontData);
            return true;
        }

        public static bool ExportMesh(AssetItem item, string exportPath)
        {
            var m_Mesh = (Mesh) item.Asset;
            if (m_Mesh.m_VertexCount <= 0)
                return false;
            if (!TryExportFile(exportPath, item, ".obj", out var exportFullPath))
                return false;
            var sb = new StringBuilder();
            sb.AppendLine("g " + m_Mesh.m_Name);

            #region Vertices

            if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
            {
                return false;
            }

            var c = 3;
            if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4)
            {
                c = 4;
            }

            for (var v = 0; v < m_Mesh.m_VertexCount; v++)
            {
                sb.AppendFormat("v {0} {1} {2}\r\n", -m_Mesh.m_Vertices[v * c], m_Mesh.m_Vertices[v * c + 1],
                    m_Mesh.m_Vertices[v * c + 2]);
            }

            #endregion

            #region UV

            if (m_Mesh.m_UV0?.Length > 0)
            {
                if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2)
                {
                    c = 2;
                }
                else if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3)
                {
                    c = 3;
                }

                for (var v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV0[v * c], m_Mesh.m_UV0[v * c + 1]);
                }
            }

            #endregion

            #region Normals

            if (m_Mesh.m_Normals?.Length > 0)
            {
                if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3)
                {
                    c = 3;
                }
                else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4)
                {
                    c = 4;
                }

                for (var v = 0; v < m_Mesh.m_VertexCount; v++)
                {
                    sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1],
                        m_Mesh.m_Normals[v * c + 2]);
                }
            }

            #endregion

            #region Face

            var sum = 0;
            for (var i = 0; i < m_Mesh.m_SubMeshes.Length; i++)
            {
                sb.AppendLine($"g {m_Mesh.m_Name}_{i}");
                var indexCount = (int) m_Mesh.m_SubMeshes[i].indexCount;
                var end = sum + indexCount / 3;
                for (var f = sum; f < end; f++)
                {
                    sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", m_Mesh.m_Indices[f * 3 + 2] + 1,
                        m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
                }

                sum = end;
            }

            #endregion

            sb.Replace("NaN", "0");
            File.WriteAllText(exportFullPath, sb.ToString());
            return true;
        }

        public static bool ExportVideoClip(AssetItem item, string exportPath)
        {
            var m_VideoClip = (VideoClip) item.Asset;
            var m_VideoData = m_VideoClip.m_VideoData.GetData();
            if (m_VideoData != null && m_VideoData.Length != 0)
            {
                if (!TryExportFile(exportPath, item, Path.GetExtension(m_VideoClip.m_OriginalPath),
                    out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_VideoData);
                return true;
            }

            return false;
        }

        public static bool ExportMovieTexture(AssetItem item, string exportPath)
        {
            var m_MovieTexture = (MovieTexture) item.Asset;
            if (!TryExportFile(exportPath, item, ".ogv", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, m_MovieTexture.m_MovieData);
            return true;
        }

        public static bool ExportSprite(AssetItem item, string exportPath)
        {
            ImageFormat format = null;
            var type = Properties.Settings.Default.convertType;
            var tga = false;
            switch (type)
            {
                case "BMP":
                    format = ImageFormat.Bmp;
                    break;
                case "PNG":
                    format = ImageFormat.Png;
                    break;
                case "JPEG":
                    format = ImageFormat.Jpeg;
                    break;
                case "TGA":
                    tga = true;
                    break;
            }

            if (!TryExportFile(exportPath, item, "." + type.ToLower(), out var exportFullPath))
                return false;
            var bitmap = ((Sprite) item.Asset).GetImage();
            if (bitmap == null) return false;
            if (tga)
            {
                var file = new TGA(bitmap);
                file.Save(exportFullPath);
            }
            else
            {
                try
                {
                    bitmap.Save(exportFullPath, format);
                    bitmap.Dispose();
                }
                catch (ExternalException)
                {
                    return ExportSprite(item, exportPath);
                }
            }

            return true;
        }

        public static bool ExportAnimator(AssetItem item, string exportPath, List<AssetItem> animationList = null)
        {
            var exportFullPath = Path.Combine(exportPath, item.Text, item.Text + ".fbx");
            if (File.Exists(exportFullPath))
            {
                exportFullPath = Path.Combine(exportPath, item.Text + item.UniqueID, item.Text + ".fbx");
            }

            var m_Animator = (Animator) item.Asset;
            var convert = animationList != null
                ? new ModelConverter(m_Animator, animationList.Select(x => (AnimationClip) x.Asset).ToArray())
                : new ModelConverter(m_Animator);
            ExportFbx(convert, exportFullPath);
            return true;
        }

        public static bool ExportAnimatorController(AssetItem item, string exportPath,
            List<AssetItem> animationList = null)
        {
            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
                return false;
            var m_AnimatorController = (AnimatorController) item.Asset;
            var nodes = new JObject
            {
                {"$Controller", JObject.FromObject(m_AnimatorController.ToType())}
            };
            var clips = new JArray();
            nodes.Add("$Clips", clips);
            foreach (var pptr in m_AnimatorController.m_AnimationClips)
            {
                if (!pptr.TryGet(out var c))
                {
                    continue;
                }

                var type = c.ToType();
                if (type == null)
                {
                    continue;
                }

                clips.Add(JObject.FromObject(type));
            }

            var str = nodes.ToString(Formatting.Indented);
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public static bool ExportAnimatorOverrideController(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
                return false;

            var m_AnimatorOverrideController = (AnimatorOverrideController) item.Asset;
            var nodes = new JObject
            {
                {"Name", m_AnimatorOverrideController.m_Name}
            };
            var clips = new JArray();
            nodes.Add("Clips", clips);
            foreach (var clip in m_AnimatorOverrideController.m_Clips)
            {
                if (!clip.m_OverrideClip.TryGet(out var c))
                {
                    continue;
                }

                // TODO: Async export
                //   - Currently has bugs
                //   - Known possible bugs: name not loaded correctly (ReadAlignedString())
                // clip.m_OverrideClip.TryGetAssetsFile(out var serializedFile);
                // c.reader = new ObjectReader(serializedFile.objectsStream[clip.m_OverrideClip.m_PathID].InitReader(c.reader.endian), item.SourceFile, c.reader.objectInfo);
                // c.reader.Reset();
                //
                // if (c.reader.byteStart > c.reader.BaseStream.Length)
                // {
                //     throw new ArgumentException($"ByteStart {c.reader.byteStart} > Stream Length {c.reader.BaseStream.Length}");
                // }

                var type = c.ToType();
                if (type == null)
                {
                    continue;
                }

                type.Insert(0, "$originalClip", clip.m_OriginalClip);
                type.Insert(1, "$overrideClip", clip.m_OverrideClip);

                clips.Add(JObject.FromObject(type));
            }

            var str = nodes.ToString(Formatting.Indented);
            File.WriteAllText(exportFullPath, str);
            return true;
        }

        public static void ExportGameObject(GameObject gameObject, string exportPath,
            List<AssetItem> animationList = null)
        {
            var convert = animationList != null
                ? new ModelConverter(gameObject, animationList.Select(x => (AnimationClip) x.Asset).ToArray())
                : new ModelConverter(gameObject);
            exportPath = exportPath + FixFileName(gameObject.m_Name) + ".fbx";
            ExportFbx(convert, exportPath);
        }

        public static void ExportGameObjectMerge(List<GameObject> gameObject, string exportPath,
            List<AssetItem> animationList = null)
        {
            var rootName = Path.GetFileNameWithoutExtension(exportPath);
            var convert = animationList != null
                ? new ModelConverter(rootName, gameObject, animationList.Select(x => (AnimationClip) x.Asset).ToArray())
                : new ModelConverter(rootName, gameObject);
            ExportFbx(convert, exportPath);
        }

        private static void ExportFbx(IImported convert, string exportPath)
        {
            var eulerFilter = Properties.Settings.Default.eulerFilter;
            var filterPrecision = (float) Properties.Settings.Default.filterPrecision;
            var exportAllNodes = Properties.Settings.Default.exportAllNodes;
            var exportSkins = Properties.Settings.Default.exportSkins;
            var exportAnimations = Properties.Settings.Default.exportAnimations;
            var exportBlendShape = Properties.Settings.Default.exportBlendShape;
            var castToBone = Properties.Settings.Default.castToBone;
            var boneSize = (int) Properties.Settings.Default.boneSize;
            var scaleFactor = (float) Properties.Settings.Default.scaleFactor;
            var fbxVersion = Properties.Settings.Default.fbxVersion;
            var fbxFormat = Properties.Settings.Default.fbxFormat;
            ModelExporter.ExportFbx(exportPath, convert, eulerFilter, filterPrecision,
                exportAllNodes, exportSkins, exportAnimations, exportBlendShape, castToBone, boneSize, scaleFactor,
                fbxVersion, fbxFormat == 1);
        }

        public static bool ExportDumpFile(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".txt", out var exportFullPath))
                return false;
            var str = item.Asset.Dump();
            if (str == null && item.Asset is MonoBehaviour m_MonoBehaviour)
            {
                var nodes = Studio.MonoBehaviourToTypeTreeNodes(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(nodes);
            }

            if (str == null) return false;
            File.WriteAllText(exportFullPath, str);

            return true;
        }

        public static bool ExportRawFile(AssetItem item, string exportPath)
        {
            if (!TryExportFile(exportPath, item, ".dat", out var exportFullPath))
                return false;
            File.WriteAllBytes(exportFullPath, item.Asset.GetRawData());
            return true;
        }

        public static bool ExportConvertFile(AssetItem item, string exportPath)
        {
            switch (item.Type)
            {
                case ClassIDType.Texture2D:
                    return ExportTexture2D(item, exportPath);
                case ClassIDType.AudioClip:
                    return ExportAudioClip(item, exportPath);
                case ClassIDType.Shader:
                    return ExportShader(item, exportPath);
                case ClassIDType.TextAsset:
                    return ExportTextAsset(item, exportPath);
                case ClassIDType.MonoBehaviour:
                    return ExportMonoBehaviour(item, exportPath);
                case ClassIDType.GameObject:
                    return ExportConvertGameObject(item, exportPath);
                case ClassIDType.Font:
                    return ExportFont(item, exportPath);
                case ClassIDType.Mesh:
                    return ExportMesh(item, exportPath);
                case ClassIDType.VideoClip:
                    return ExportVideoClip(item, exportPath);
                case ClassIDType.MovieTexture:
                    return ExportMovieTexture(item, exportPath);
                case ClassIDType.Sprite:
                    return ExportSprite(item, exportPath);
                case ClassIDType.Animator:
                    return ExportAnimator(item, exportPath);
                case ClassIDType.AnimatorController:
                    return ExportAnimatorController(item, exportPath);
                case ClassIDType.AnimatorOverrideController:
                    return ExportAnimatorOverrideController(item, exportPath);
                default:
                    return ExportRawFile(item, exportPath);
            }
        }

        public static string FixFileName(string str)
        {
            return str.Length >= 260
                ? Path.GetRandomFileName()
                : Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }
    }
}