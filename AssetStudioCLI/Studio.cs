﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AssetStudio;
using static AssetStudioCLI.Exporter;
using Object = AssetStudio.Object;

namespace AssetStudioCLI
{
    internal enum ExportType
    {
        Convert,
        Raw,
        Dump
    }

    internal enum ProcessType
    {
        Async,
        Sync
    }

    internal static class Studio
    {
        public static readonly AssetsManager assetsManager = new AssetsManager();
        public static readonly AssemblyLoader assemblyLoader = new AssemblyLoader();
        public static readonly List<AssetItem> exportableAssets = new List<AssetItem>();
        public static List<AssetItem> visibleAssets = new List<AssetItem>();
        public static string assemblyReferenceFolder = "";
        public static string exportSuffix = "";
        public static bool skipExists = false;
        internal static Action<string> StatusStripUpdate = x => { };

        public static int ExtractFolder(string path, string savePath)
        {
            var extractedCount = 0;
            Progress.Reset();
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var fileOriPath = Path.GetDirectoryName(file);
                var fileSavePath = fileOriPath?.Replace(path, savePath);
                extractedCount += ExtractFile(file, fileSavePath);
                Progress.Report(i + 1, files.Length);
            }

            return extractedCount;
        }

        public static int ExtractFile(string[] fileNames, string savePath)
        {
            var extractedCount = 0;
            Progress.Reset();
            for (var i = 0; i < fileNames.Length; i++)
            {
                var fileName = fileNames[i];
                extractedCount += ExtractFile(fileName, savePath);
                Progress.Report(i + 1, fileNames.Length);
            }

            return extractedCount;
        }

        public static int ExtractFile(string fileName, string savePath)
        {
            var extractedCount = 0;
            var type = ImportHelper.CheckFileType(fileName, out var stream);
            if (type == FileType.BundleFile)
                extractedCount += ExtractBundleFile(fileName, stream, savePath);
            else if (type == FileType.WebFile)
                extractedCount += ExtractWebDataFile(fileName, stream, savePath);
            else
                stream.Dispose();
            return extractedCount;
        }

        private static int ExtractBundleFile(string bundleFilePath, EndianBinaryStream stream, string savePath)
        {
            StatusStripUpdate($"Decompressing {Path.GetFileName(bundleFilePath)} ...");
            var bundleFile = new BundleFile(stream, bundleFilePath);
            stream.Dispose();
            if (bundleFile.fileList.Length <= 0) return 0;

            var extractPath = Path.Combine(savePath, Path.GetFileName(bundleFilePath) + "_unpacked");
            return ExtractStreamFile(extractPath, bundleFile.fileList);
        }

        private static int ExtractWebDataFile(string webFilePath, EndianBinaryStream stream, string savePath)
        {
            StatusStripUpdate($"Decompressing {Path.GetFileName(webFilePath)} ...");
            var webFile = new WebFile(stream.InitReader());
            stream.Dispose();
            if (webFile.fileList.Length <= 0) return 0;

            var extractPath = Path.Combine(savePath, Path.GetFileName(webFilePath) + "_unpacked");
            return ExtractStreamFile(extractPath, webFile.fileList);
        }

        private static int ExtractStreamFile(string extractPath, IEnumerable<StreamFile> fileList)
        {
            var extractedCount = 0;
            foreach (var file in fileList)
            {
                var filePath = Path.Combine(extractPath, file.fileName);
                if (!Directory.Exists(extractPath))
                {
                    Directory.CreateDirectory(extractPath);
                }

                if (!File.Exists(filePath))
                {
                    using (var fileStream = File.Create(filePath))
                    {
                        file.stream.CopyTo(fileStream);
                    }

                    extractedCount += 1;
                }

                file.stream.Dispose();
            }

            return extractedCount;
        }

        public static (string, List<TreeNode>) BuildAssetData()
        {
            StatusStripUpdate("Building asset list...");

            string productName = null;
            var objectCount = assetsManager.assetsFileList.Sum(x => x.objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var containers = new List<(PPtr<Object>, string)>();
            var i = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                foreach (var asset in assetsFile.objects)
                {
                    var assetItem = new AssetItem(asset);
                    objectAssetItemDic.Add(asset, assetItem);
                    assetItem.UniqueID = " #" + i;
                    var exportable = false;
                    switch (asset)
                    {
                        case GameObject m_GameObject:
                            assetItem.Text = m_GameObject.m_Name;
                            break;
                        case Texture2D m_Texture2D:
                            if (!string.IsNullOrEmpty(m_Texture2D.m_StreamData?.path))
                                assetItem.FullSize = asset.byteSize + m_Texture2D.m_StreamData.size;
                            assetItem.Text = m_Texture2D.m_Name;
                            exportable = true;
                            break;
                        case AudioClip m_AudioClip:
                            if (!string.IsNullOrEmpty(m_AudioClip.m_Source))
                                assetItem.FullSize = asset.byteSize + m_AudioClip.m_Size;
                            assetItem.Text = m_AudioClip.m_Name;
                            exportable = true;
                            break;
                        case VideoClip m_VideoClip:
                            if (!string.IsNullOrEmpty(m_VideoClip.m_OriginalPath))
                                assetItem.FullSize = asset.byteSize + (long) m_VideoClip.m_ExternalResources.m_Size;
                            assetItem.Text = m_VideoClip.m_Name;
                            exportable = true;
                            break;
                        case Shader m_Shader:
                            assetItem.Text = m_Shader.m_ParsedForm?.m_Name ?? m_Shader.m_Name;
                            exportable = true;
                            break;
                        case Mesh _:
                        case TextAsset _:
                        case AnimationClip _:
                        case Font _:
                        case MovieTexture _:
                        case Sprite _:
                            assetItem.Text = ((NamedObject) asset).m_Name;
                            exportable = true;
                            break;
                        case Animator m_Animator:
                            if (m_Animator.m_GameObject.TryGet(out var gameObject))
                            {
                                assetItem.Text = gameObject.m_Name;
                            }

                            exportable = true;
                            break;
                        case MonoBehaviour m_MonoBehaviour:
                            if (m_MonoBehaviour.m_Name == "" && m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                            {
                                assetItem.Text = m_Script.m_ClassName;
                            }
                            else
                            {
                                assetItem.Text = m_MonoBehaviour.m_Name;
                            }

                            exportable = true;
                            break;
                        case PlayerSettings m_PlayerSettings:
                            productName = m_PlayerSettings.productName;
                            break;
                        case AssetBundle m_AssetBundle:
                            foreach (var m_Container in m_AssetBundle.m_Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (var k = preloadIndex; k < preloadEnd; k++)
                                {
                                    containers.Add((m_AssetBundle.m_PreloadTable[k], m_Container.Key));
                                }
                            }

                            assetItem.Text = m_AssetBundle.m_Name;
                            break;
                        case ResourceManager m_ResourceManager:
                            foreach (var m_Container in m_ResourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }

                            break;
                        case NamedObject m_NamedObject:
                            assetItem.Text = m_NamedObject.m_Name;
                            break;
                    }

                    if (assetItem.Text == "")
                    {
                        assetItem.Text = assetItem.TypeString + assetItem.UniqueID;
                    }

                    if (Properties.Settings.Default.displayAll || exportable)
                    {
                        exportableAssets.Add(assetItem);
                    }

                    Progress.Report(++i, objectCount);
                }
            }

            foreach (var (pptr, container) in containers)
            {
                if (!pptr.TryGet(out var obj)) continue;
                if (objectAssetItemDic[obj].Container != string.Empty) continue;
                objectAssetItemDic[obj].Container = container;
            }

            containers.Clear();

            visibleAssets = exportableAssets;

            StatusStripUpdate("Building tree structure...");

            var treeNodeCollection = new List<TreeNode>();
            var treeNodeDictionary = new Dictionary<GameObject, GameObjectTreeNode>();
            var assetsFileCount = assetsManager.assetsFileList.Count;
            var j = 0;
            Progress.Reset();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                var fileNode = new GameObjectTreeNode(assetsFile.fileName); //RootNode

                foreach (var obj in assetsFile.objects)
                {
                    if (!(obj is GameObject m_GameObject)) continue;

                    if (!treeNodeDictionary.TryGetValue(m_GameObject, out var currentNode))
                    {
                        currentNode = new GameObjectTreeNode(m_GameObject);
                        treeNodeDictionary.Add(m_GameObject, currentNode);
                    }

                    foreach (var pptr in m_GameObject.m_Components)
                    {
                        if (!pptr.TryGet(out var m_Component)) continue;
                        objectAssetItemDic[m_Component].TreeNode = currentNode;

                        switch (m_Component)
                        {
                            case MeshFilter m_MeshFilter:
                            {
                                if (m_MeshFilter.m_Mesh.TryGet(out var m_Mesh))
                                {
                                    objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                }

                                break;
                            }
                            case SkinnedMeshRenderer m_SkinnedMeshRenderer:
                            {
                                if (m_SkinnedMeshRenderer.m_Mesh.TryGet(out var m_Mesh))
                                {
                                    objectAssetItemDic[m_Mesh].TreeNode = currentNode;
                                }

                                break;
                            }
                        }
                    }

                    var parentNode = fileNode;

                    if (m_GameObject.m_Transform != null)
                    {
                        if (m_GameObject.m_Transform.m_Father.TryGet(out var m_Father))
                        {
                            if (m_Father.m_GameObject.TryGet(out var parentGameObject))
                            {
                                if (!treeNodeDictionary.TryGetValue(parentGameObject, out parentNode))
                                {
                                    parentNode = new GameObjectTreeNode(parentGameObject);
                                    treeNodeDictionary.Add(parentGameObject, parentNode);
                                }
                            }
                        }
                    }

                    parentNode.Nodes.Add(currentNode);
                }

                if (fileNode.Nodes.Count > 0)
                {
                    treeNodeCollection.Add(fileNode);
                }

                Progress.Report(++j, assetsFileCount);
            }

            treeNodeDictionary.Clear();

            objectAssetItemDic.Clear();

            return (productName, treeNodeCollection);
        }

        public static Dictionary<string, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
        {
            var typeMap = new Dictionary<string, SortedDictionary<int, TypeTreeItem>>();
            foreach (var assetsFile in assetsManager.assetsFileList)
            {
                if (typeMap.TryGetValue(assetsFile.unityVersion, out var curVer))
                {
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Nodes != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        curVer[key] = new TypeTreeItem(key, type.m_Nodes);
                    }
                }
                else
                {
                    var items = new SortedDictionary<int, TypeTreeItem>();
                    foreach (var type in assetsFile.m_Types.Where(x => x.m_Nodes != null))
                    {
                        var key = type.classID;
                        if (type.m_ScriptTypeIndex >= 0)
                        {
                            key = -1 - type.m_ScriptTypeIndex;
                        }

                        items[key] = new TypeTreeItem(key, type.m_Nodes);
                    }

                    typeMap.Add(assetsFile.unityVersion, items);
                }
            }

            return typeMap;
        }

        private static string GetExportPath(string savePath, AssetItem asset)
        {
            string exportPath;
            switch (Properties.Settings.Default.assetGroupOption)
            {
                case 0: //type name
                    exportPath = Path.Combine(savePath, asset.TypeString);
                    break;
                case 1: //container path
                    exportPath = Path.Combine(savePath, !string.IsNullOrEmpty(asset.Container)
                        ? Path.GetDirectoryName(asset.Container)
                        : "unknown");
                    break;
                case 2: //source file
                    exportPath = Path.Combine(savePath, asset.SourceFile.fullName + "_export");
                    break;
                default:
                    exportPath = savePath;
                    break;
            }

            exportPath += Path.DirectorySeparatorChar;

            return exportPath;
        }

        private static void ExportAsset(string exportPath, AssetItem asset, ExportType exportType,
            ref List<string> exportFailedNames)
        {
            StatusStripUpdate(
                $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.ffffff}: Exporting {asset.TypeString}: {asset.Text}");
            switch (exportType)
            {
                case ExportType.Raw:
                    if (!ExportRawFile(asset, exportPath))
                    {
                        exportFailedNames.Add(asset.SourceFile.originalPath);
                    }

                    break;
                case ExportType.Dump:
                    if (!ExportDumpFile(asset, exportPath))
                    {
                        exportFailedNames.Add(asset.SourceFile.originalPath);
                    }

                    break;
                case ExportType.Convert:
                    if (!ExportConvertFile(asset, exportPath))
                    {
                        exportFailedNames.Add(asset.SourceFile.originalPath);
                    }

                    break;
            }
        }

        private static void ExportAssetsAsync(string savePath, IEnumerable<AssetItem> toExportAssets,
            ExportType exportType, List<string> exportFailedNames)
        {
            var tasks = toExportAssets
                .GroupBy(asset => asset.SourceFile.originalPath)
                .Select(group =>
                {
                    return Task.Factory.StartNew(() =>
                    {
                        foreach (var asset in group)
                        {
                            ExportAsset(GetExportPath(savePath, asset), asset, exportType, ref exportFailedNames);
                        }
                    });
                })
                .ToArray();

            Task.WaitAll(tasks);
        }

        private static void ExportAssetsSync(string savePath, IEnumerable<AssetItem> toExportAssets,
            ExportType exportType, List<string> exportFailedNames)
        {
            foreach (var asset in toExportAssets)
            {
                ExportAsset(GetExportPath(savePath, asset), asset, exportType, ref exportFailedNames);
            }
        }

        public static void ExportAssets(string savePath, List<AssetItem> toExportAssets, ExportType exportType,
            ProcessType processType)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            var exportFailedNames = new List<string>();

            switch (processType)
            {
                case ProcessType.Async:
                    // TODO: Async export bugged for assets that uses common objects (AnimatorOverrideController)
                    try
                    {
                        ExportAssetsAsync(savePath, toExportAssets, exportType, exportFailedNames);
                    }
                    catch (AggregateException e)
                    {
                        StatusStripUpdate(
                            $"Error occurred during async processing, fall back to sync processing. ({e})");
                        ExportAssetsSync(savePath, toExportAssets, exportType, exportFailedNames);
                    }

                    break;
                case ProcessType.Sync:
                    ExportAssetsSync(savePath, toExportAssets, exportType, exportFailedNames);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processType), processType, "Undefined process type");
            }

            StatusStripUpdate(toExportAssets.Count == 0 ? "Nothing exported." : "Finished exporting assets.");

            if (exportFailedNames.Count > 0)
            {
                StatusStripUpdate(
                    $"{exportFailedNames.Count} assets were skipped: {string.Join(", ", exportFailedNames)}");
            }

            if (Properties.Settings.Default.openAfterExport && toExportAssets.Count > 0)
            {
                Process.Start(savePath);
            }
        }

        public static void ExportSplitObjects(string savePath, TreeNodeCollection nodes)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var count = nodes.Cast<TreeNode>().Sum(x => x.Nodes.Count);
                var k = 0;
                Progress.Reset();
                foreach (GameObjectTreeNode node in nodes)
                {
                    // Traverse first-level nodes
                    foreach (var treeNode in node.Nodes)
                    {
                        var j = (GameObjectTreeNode) treeNode;
                        // Collect all nodes
                        var gameObjects = new List<GameObject>();
                        CollectNode(j, gameObjects);
                        // Skip some unnecessary objects
                        if (gameObjects.All(x => x.m_SkinnedMeshRenderer == null && x.m_MeshFilter == null))
                        {
                            Progress.Report(++k, count);
                            continue;
                        }

                        // Fix illegal file names
                        var filename = FixFileName(j.Text);
                        // Store each files in its directory
                        var targetPath = $"{savePath}{filename}\\";
                        // Handle duplicated file names
                        for (var i = 1;; i++)
                        {
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = $"{savePath}{filename} ({i})\\";
                            }
                            else
                            {
                                break;
                            }
                        }

                        Directory.CreateDirectory(targetPath);
                        // Export FBX
                        StatusStripUpdate($"Exporting {filename}.fbx");
                        try
                        {
                            ExportGameObject(j.gameObject, targetPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Export GameObject:{j.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                        }

                        Progress.Report(++k, count);
                        StatusStripUpdate($"Finished exporting {filename}.fbx");
                    }
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    Process.Start(savePath);
                }

                StatusStripUpdate("Finished");
            });
        }

        private static void CollectNode(GameObjectTreeNode node, ICollection<GameObject> gameObjects)
        {
            gameObjects.Add(node.gameObject);
            foreach (var i in node.Nodes.Cast<GameObjectTreeNode>())
            {
                CollectNode(i, gameObjects);
            }
        }

        public static void ExportAnimatorWithAnimationClip(AssetItem animator, List<AssetItem> animationList,
            string exportPath)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                Progress.Reset();
                StatusStripUpdate($"Exporting {animator.Text}");
                try
                {
                    ExportAnimator(animator, exportPath, animationList);
                    if (Properties.Settings.Default.openAfterExport)
                    {
                        Process.Start(exportPath);
                    }

                    Progress.Report(1, 1);
                    StatusStripUpdate($"Finished exporting {animator.Text}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export Animator:{animator.Text} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("Error in export");
                }
            });
        }

        public static void ExportObjectsWithAnimationClip(string exportPath, List<TreeNode> nodes,
            List<AssetItem> animationList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var gameObjects = new List<GameObject>();
                GetSelectedParentNode(nodes, gameObjects);
                if (gameObjects.Count > 0)
                {
                    var count = gameObjects.Count;
                    var i = 0;
                    Progress.Reset();
                    foreach (var gameObject in gameObjects)
                    {
                        StatusStripUpdate($"Exporting {gameObject.m_Name}");
                        try
                        {
                            ExportGameObject(gameObject, exportPath, animationList);
                            StatusStripUpdate($"Finished exporting {gameObject.m_Name}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Export GameObject:{gameObject.m_Name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                            StatusStripUpdate("Error in export");
                        }

                        Progress.Report(++i, count);
                    }

                    if (Properties.Settings.Default.openAfterExport)
                    {
                        Process.Start(exportPath);
                    }
                }
                else
                {
                    StatusStripUpdate("No Object can be exported.");
                }
            });
        }

        public static void ExportObjectsMergeWithAnimationClip(string exportPath, List<GameObject> gameObjects,
            List<AssetItem> animationList = null)
        {
            ThreadPool.QueueUserWorkItem(state =>
            {
                var name = Path.GetFileName(exportPath);
                Progress.Reset();
                StatusStripUpdate($"Exporting {name}");
                try
                {
                    ExportGameObjectMerge(gameObjects, exportPath, animationList);
                    Progress.Report(1, 1);
                    StatusStripUpdate($"Finished exporting {name}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export Model:{name} error\r\n{ex.Message}\r\n{ex.StackTrace}");
                    StatusStripUpdate("Error in export");
                }

                if (Properties.Settings.Default.openAfterExport)
                {
                    Process.Start(Path.GetDirectoryName(exportPath));
                }
            });
        }

        public static void GetSelectedParentNode(IEnumerable<TreeNode> nodes, List<GameObject> gameObjects)
        {
            foreach (var i in nodes.Cast<GameObjectTreeNode>())
            {
                if (i.Checked)
                {
                    gameObjects.Add(i.gameObject);
                }
                else
                {
                    GetSelectedParentNode(i.Nodes, gameObjects);
                }
            }
        }

        public static List<TypeTreeNode> MonoBehaviourToTypeTreeNodes(MonoBehaviour m_MonoBehaviour)
        {
            if (!assemblyLoader.Loaded)
            {
                if (!string.IsNullOrEmpty(assemblyReferenceFolder))
                {
                    assemblyLoader.Load(assemblyReferenceFolder);
                }
                else
                {
                    assemblyLoader.Loaded = true;
                }
            }

            return m_MonoBehaviour.ConvertToTypeTreeNodes(assemblyLoader);
        }

        public static string DumpAsset(Object obj)
        {
            var str = obj.Dump();
            if (str == null && obj is MonoBehaviour m_MonoBehaviour)
            {
                var nodes = MonoBehaviourToTypeTreeNodes(m_MonoBehaviour);
                str = m_MonoBehaviour.Dump(nodes);
            }

            return str;
        }

        public static void LoadAsset(string filePath)
        {
            assetsManager.LoadFiles(filePath);
        }
    }
}