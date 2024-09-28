﻿using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using LuaSTGEditorSharp.Execution;
using LuaSTGEditorSharp.Plugin;
using LuaSTGEditorSharp.Toolbox;
using LuaSTGEditorSharp.Windows;
using LuaSTGEditorSharp.Windows.Input;
using LuaSTGEditorSharp.EditorData.Exception;
using LuaSTGEditorSharp.EditorData;
using LuaSTGEditorSharp.EditorData.Document;
using LuaSTGEditorSharp.EditorData.Node;
using LuaSTGEditorSharp.EditorData.Node.General;
using LuaSTGEditorSharp.EditorData.Node.Advanced;
using LuaSTGEditorSharp.EditorData.Commands;
using LuaSTGEditorSharp.EditorData.Commands.Factory;
using Newtonsoft.Json;

using Path = System.IO.Path;
using System.Windows.Threading;
using LuaSTGEditorSharp.Properties;
using NetSparkleUpdater;
using NetSparkleUpdater.SignatureVerifiers;
using System.Runtime.CompilerServices;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using LuaSTGEditorSharp.Notifications;
using DiscordRPC;
using DiscordRPC.Logging;
using Button = System.Windows.Controls.Button;
using ToastNotifications.Lifetime.Clear;

namespace LuaSTGEditorSharp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IMainWindow
    {
        DocumentCollection Documents { get; } = new DocumentCollection();

        private string debugString = "";

        public ObservableCollection<ToolboxTab> toolboxData;
        public ObservableCollection<ToolboxTab> ToolboxData { get => toolboxData; }

        ObservableCollection<FileDirectoryModel> PresetsGetList { get; } = new ObservableCollection<FileDirectoryModel>();
        ObservableCollection<PluginTool> PluginTools { get; } = new ObservableCollection<PluginTool>();

        private CommandTypeFac insertState = new AfterFac();

        //private object locker = new object();
        
        public bool IsBeforeState { get => insertState.GetType() == typeof(BeforeFac); }
        public bool IsAfterState { get => insertState.GetType() == typeof(AfterFac); }
        public bool IsChildState { get => insertState.GetType() == typeof(ChildFac); }
        public bool IsParentState { get => insertState.GetType() == typeof(ParentFac); }

        public ObservableCollection<MessageBase> Messages { get => MessageContainer.Messages; }

        private bool packagingLocked = false;

        public string DebugString
        {
            get => debugString;
            set
            {
                debugString = value;
                RaiseProertyChanged("DebugString");
            }
        }

        public TreeNode clipBoard = null;

        public TreeNode selectedNode = null;

        public TreeNode SelectedNode
        {
            get => selectedNode;
            set
            {
                selectedNode = value;
                RaiseProertyChanged("SelectedNode");
            }
        }

        public DocumentData ActivatedWorkSpaceData
        {
            get
            {
                return (DocumentData)docTabs?.SelectedItem;
            }
        }

        public TreeView workSpace;

        private BackgroundWorker CompileWorker;

        public SparkleUpdater Sparkle;

        //public Notifier _Notifier;

        public DiscordRpcClient DiscordClient;

        public MainWindow()
        {
            toolbox = PluginHandler.Plugin.GetToolbox(this);
            toolboxData = toolbox.ToolboxTabs;
            InitDict();
            InitializeComponent();
            comboDict.ItemsSource = toolbox.nodeNameList;
            this.docTabs.ItemsSource = Documents;
            EditorConsole.ItemsSource = Messages;
            GetPresets();
            GetPluginTools();
            presetsMenu.ItemsSource = PresetsGetList;
            CompileWorker = this.FindResource("CompileWorker") as BackgroundWorker;

            SetupDiscordRpc();
            //SetupNotifier();
            StartSparkle();
            SetupAutoSave();

            Sparkle.StartLoop((App.Current as App).CheckUpdateAtLaunch, (App.Current as App).CheckUpdateAtLaunch);
        }


        #region Editor Initialization

        private void SetupDiscordRpc()
        {
            if (!(App.Current as App).UseDiscordRpc)
                return;

            DiscordClient = new DiscordRpcClient("1263260551153319996");

            DiscordClient.Initialize();

            DiscordRpcReset();
        }

        public void DiscordRpcReset()
        {
            DiscordClient?.SetPresence(new RichPresence()
            {
                State = "Idle",
                Timestamps = Timestamps.Now,
                Assets = new Assets()
                {
                    LargeImageKey = "sharpxicon",
                    LargeImageText = "Not doing anything"
                }
            });
        }

        private void StartSparkle()
        {
            Sparkle = new SparkleUpdater(
                "https://raw.githubusercontent.com/RyannThi/LuaSTG-Editor-Sharp-X/main/AppCast.xaml",
                new Ed25519Checker(NetSparkleUpdater.Enums.SecurityMode.Unsafe)
            )
            {
                UIFactory = new NetSparkleUpdater.UI.WPF.UIFactory(Icon),
                RelaunchAfterUpdate = true,
                CustomInstallerArguments = "",
                ShowsUIOnMainThread = true,
                CheckServerFileName = false // Why was that not in the docs
            };
            Sparkle.PreparingToExit += SparkleCloseFiles;
        }

        private void SetupAutoSave()
        {
            if ((App.Current as App).UseAutoSave)
            {
                var autoSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes((App.Current as App).AutoSaveTimer)
                };
                autoSaveTimer.Tick += delegate (object sender, EventArgs e)
                {
                    AutoSaveAndBackup();
                };
                autoSaveTimer.Start();
            }
        }

        /*private void SetupNotifier()
        {
            _Notifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.TopRight,
                    offsetX: 20,
                    offsetY: 180
                );

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(3),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5)
                );

                cfg.Dispatcher = Application.Current.Dispatcher;
            });
        }*/

        private void SparkleCloseFiles(object sender, CancelEventArgs e)
        {
            foreach (DocumentData doc in Documents)
                CloseFile(doc);
        }

        #endregion
        #region Editor Execution

        private bool TestError()
        {
            App currentApp = Application.Current as App;
            if (ActivatedWorkSpaceData != null)
            {
                var docDir = Path.GetDirectoryName(ActivatedWorkSpaceData.DocPath);
                ActivatedWorkSpaceData.GatherCompileInfo(currentApp);
                if (currentApp.UseFolderPacking)
                {
                    if (docDir == ActivatedWorkSpaceData.CompileProcess.targetZipPath)
                    {
                        MessageBox.Show("Project files are under output directory.\n" +
                            "The output directory will be deleted before build. DO NOT save project files in the output directory!",
                            "LuaSTG Editor Sharp X", MessageBoxButton.OK, MessageBoxImage.Error);
                        return true;
                    }
                }
            }
            if (!MessageContainer.IsNoError())
            {
                tabMessage.IsSelected = true;
                MessageBox.Show("Errors are found in the editor. The project cannot be compiled if any error is present."
                    , "LuaSTG Editor Sharp X", MessageBoxButton.OK, MessageBoxImage.Error);
                return true;
            }
            return false;
        }

        private void ViewCode()
        {
            try
            {
                propData.CommitEdit();
                if (TestError()) return;
                ActivatedWorkSpaceData.GatherCompileInfo(App.Current as App);
                var w = new CodePreviewWindow(string.Concat(selectedNode.ToLua(0)));
                w.ShowDialog();
                //SaveXML();
            }
            catch (Exception e) { MessageBox.Show(e.ToString()); }
        }

        private void CreateInvoke(TreeNode newNode)
        {
            propData.CommitEdit();
            AttrItem ai = newNode.GetCreateInvoke();
            if (ai != null)
            {
                IInputWindow iw = InputWindowSelector.SelectInputWindow(ai, ai.EditWindow, ai.AttrInput);
                if (iw.ShowDialog() == true)
                {
                    ActivatedWorkSpaceData.AddAndExecuteCommand(new EditAttrCommand(ai, ai.AttrInput, iw.Result));
                    var a = propData.ItemsSource;
                    propData.ItemsSource = null;
                    propData.ItemsSource = a;
                }
            }
        }

        private void ShowEditWindow()
        {
            propData.CommitEdit();
            AttrItem ai = selectedNode?.GetRCInvoke();
            if (ai != null)
            {
                IInputWindow iw = InputWindowSelector.SelectInputWindow(ai, ai.EditWindow, ai.AttrInput);
                if (iw.ShowDialog() == true)
                {
                    ActivatedWorkSpaceData.AddAndExecuteCommand(new EditAttrCommand(ai, ai.AttrInput, iw.Result));
                    var a = propData.ItemsSource;
                    propData.ItemsSource = null;
                    propData.ItemsSource = a;
                }
            }
        }

        static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);

            return source;
        }

        #endregion
        #region Node Execution

        private void CutNode()
        {
            clipBoard = (TreeNode)selectedNode.Clone();
            TreeNode prev = selectedNode.GetNearestEdited();
            ActivatedWorkSpaceData.AddAndExecuteCommand(new DeleteCommand(selectedNode));
            //_Notifier.ShowInfo("Node cut!");
            if (prev != null) Reveal(prev);
        }

        private void CopyNode()
        {
            clipBoard = (TreeNode)selectedNode.Clone();
            //_Notifier.ShowInfo("Node copied!");
        }

        private void PasteNode()
        {
            try
            {
                TreeNode node = (TreeNode)clipBoard.Clone();
                node.FixParentDoc(ActivatedWorkSpaceData);
                Insert(node, false);
                //_Notifier.ShowInfo("Node pasted!");
            }
            catch { }
        }

        private void Undo()
        {
            ActivatedWorkSpaceData.Undo();
        }

        private void Redo()
        {
            ActivatedWorkSpaceData.Redo();
        }

        private void DeleteNode()
        {
            TreeNode prev = selectedNode.GetNearestEdited();
            ActivatedWorkSpaceData.AddAndExecuteCommand(new DeleteCommand(selectedNode));
            if (prev != null) Reveal(prev);
        }

        private void GotoLine(int line)
        {
            int c = 0;
            ActivatedWorkSpaceData.GatherCompileInfo(App.Current as App);
            foreach (Tuple<int, TreeNode> tuple in ActivatedWorkSpaceData.TreeNodes[0].GetLines())
            {
                c += tuple.Item1;
                if (c >= line)
                {
                    Reveal(tuple.Item2);
                    break;
                }
            }
        }

        public void Reveal(TreeNode node)
        {
            if (node == null) return;
            TreeNode temp = node.Parent;
            node.parentWorkSpace.IsSelected = true;
            node.parentWorkSpace.TreeNodes[0].ClearChildSelection();
            Stack<TreeNode> sta = new Stack<TreeNode>();
            while (temp != null)
            {
                sta.Push(temp);
                temp = temp.Parent;
            }

            while (sta.Count > 0)
            {
                sta.Pop().IsExpanded = true;
            }
            node.IsSelected = true;
        }

        private void FoldRegion()
        {
            Region beg = selectedNode as Region;
            Region end = null;
            ObservableCollection<TreeNode> toFold = new ObservableCollection<TreeNode>();
            TreeNode p = beg.Parent;
            bool inSel = false;
            for (int i = 0; i < p.Children.Count; i++)
            {
                if (p.Children[i] != beg && p.Children[i] is Region)
                {
                    inSel = false;
                    end = p.Children[i] as Region;
                }
                if (inSel)
                {
                    toFold.Add(p.Children[i]);
                }
                if (p.Children[i] == beg)
                {
                    inSel = true;
                }
            }
            ActivatedWorkSpaceData.AddAndExecuteCommand(new FoldRegionCommand(toFold, beg, end));
        }

        private void UnfoldAsRegion()
        {
            ActivatedWorkSpaceData.AddAndExecuteCommand(new UnfoldAsRegionCommand(selectedNode));
        }

        #endregion
        #region File Execution

        private bool CloseFile(DocumentData DocumentToRemove)
        {
            if (DocumentToRemove.IsUnsaved)
            {
                switch (MessageBox.Show("Do you want to save \"" + DocumentToRemove.RawDocName
                    + "\"? ", "LuaSTG Editor Sharp X", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
                {
                    case MessageBoxResult.Yes:
                        if (SaveDoc(DocumentToRemove))
                        {
                            Documents.Remove(DocumentToRemove);
                            DocumentToRemove.OnClosing();
                            if (propData.ItemsSource is ObservableCollection<AttrItem> ai)
                            {
                                if (ai.Count > 0 && ai[0].Parent.parentWorkSpace == DocumentToRemove)
                                {
                                    propData.ItemsSource = null;
                                }
                            }
                        }
                        return true;
                    case MessageBoxResult.No:
                        break;
                    default:
                        return false;
                }
            }
            Documents.Remove(DocumentToRemove);
            DocumentToRemove.OnClosing();
            if (propData.ItemsSource is ObservableCollection<AttrItem> oai)
            {
                if (oai.Count > 0 && oai[0].Parent.parentWorkSpace == DocumentToRemove)
                {
                    propData.ItemsSource = null;
                }
            }
            return true;
        }

        private void NewDoc()
        {
            NewWindow nw = new NewWindow();
            if (nw.ShowDialog() == true)
            {
                string fullPathClone = Path.GetFullPath(nw.SelectedPath);
                CloneDocFromPath(nw.FileName, fullPathClone,
                    new ProjSettings(ActivatedWorkSpaceData, "", nw.Author, nw.AllowPR.ToString().ToLower(), nw.AllowSCPR.ToString().ToLower(), nw.ModVersion.ToString().ToLower()));
            }
        }

        private void OpenDoc()
        {
            var loadFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                InitialDirectory = (App.Current as App).SLDir,
                Filter = "LuaSTG Sharp Editor File (*.lstges, *.lstgproj)|*.lstges;*.lstgproj",
                Multiselect = true
            };
            if (loadFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) return;
            for (int i = 0; i < loadFileDialog.FileNames.Length; i++)
            {
                if (!IsOpened(loadFileDialog.FileNames[i])) OpenDocFromPath(loadFileDialog.SafeFileNames[i], loadFileDialog.FileNames[i]);
                (App.Current as App).SLDir = Path.GetDirectoryName(loadFileDialog.FileNames[i]);
            }
        }

        private bool IsOpened(string path)
        {
            foreach (DocumentData doc in Documents)
            {
                if (!string.IsNullOrEmpty(doc.DocPath) && Path.GetFullPath(doc.DocPath) == Path.GetFullPath(path))
                {
                    return true;
                }
            }
            return false;
        }

        public void OpenDocByFile(string arg)
        {
            if (!IsOpened(arg)) OpenDocFromPath(Path.GetFileName(arg), arg);
        }

        public async void OpenDocFromPath(string name, string path)
        {
            try
            {
                DocumentData newDoc = DocumentData.GetNewByExtension(Path.GetExtension(path), Documents.MaxHash, name, path);
                Documents.AddAndAllocHash(newDoc);
                TreeNode t = await DocumentData.CreateNodeFromFileAsync(path, newDoc);
                newDoc.TreeNodes.Add(t);
                t.RaiseCreate(new OnCreateEventArgs() { parent = null });
                newDoc.OnOpening();
                //newDoc.TreeNodes[0].FixBan();
                newDoc.OriginalMeta.PropertyChanged += newDoc.OnEditing;
            }
            catch (JsonException e)
            {
                MessageBox.Show("Failed to open document. Please check whether the targeted file is in current version.\n"
                    + e.ToString()
                    , "LuaSTG Editor Sharp X", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async void CloneDocFromPath(string name, string path, ProjSettings settings)
        {
            try
            {
                DocumentData newDoc = DocumentData.GetNewByExtension(Path.GetExtension(path), Documents.MaxHash, name, path);
                newDoc.DocPath = "";
                Documents.AddAndAllocHash(newDoc);
                TreeNode t = await DocumentData.CreateNodeFromFileAsync(path, newDoc);
                newDoc.TreeNodes.Add(t);
                t.RaiseCreate(new OnCreateEventArgs() { parent = null });
                foreach (TreeNode node in t.Children)
                {
                    if (node is ProjSettings)
                    {
                        for (int i = 0; i < node.attributes.Count; i++)
                        {
                            node.attributes[i].AttrInput = settings.attributes[i].AttrInput;
                        }
                    }
                    else if (node is EditorVersion)
                    {
                        node.attributes[0].AttrInput = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    }
                }
                newDoc.OnOpening();
                //newDoc.TreeNodes[0].FixBan();
                newDoc.OriginalMeta.PropertyChanged += newDoc.OnEditing;
            }
            catch (JsonException e)
            {
                MessageBox.Show("Failed to open document. Please check whether the targeted file is in current version.\n"
                    + e
                    , "LuaSTG Editor Sharp X", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveActiveFile()
        {
            return SaveDoc(ActivatedWorkSpaceData);
        }

        private bool SaveDoc(DocumentData doc)
        {
            propData.CommitEdit();
            bool success = doc.Save(App.Current as App);
            //if (success)
            //    _Notifier.ShowSuccess("Project saved!");
            return success;
        }

        private void SaveActiveFileAs()
        {
            propData.CommitEdit();
            ActivatedWorkSpaceData.Save(App.Current as App, true);
                //_Notifier.ShowSuccess("Project saved!");
        }

        /// <summary>
        /// Auto save all opened projects every X minutes and create a backup.
        /// </summary>
        private void AutoSaveAndBackup()
        {
            // Copy the project files and treat them as the backup, then, save all the opened documents.
            foreach (DocumentData doc in Documents)
            {
                try
                {
                    //_Notifier.ShowInfo("Auto saving...");
                    FileInfo fiDoc = new(doc.DocPath); // Auto Save Proj
                    fiDoc.CopyTo(doc.DocPath + ".backup", true);
                    FileInfo fiMeta = new(doc.DocPath + ".meta"); // Auto Save Proj Meta (imported files)
                    fiMeta.CopyTo(doc.DocPath + ".meta.backup", true);
                    SaveDoc(doc);
                    //_Notifier.ShowSuccess("Auto save successful.");
                }
                catch (Exception ex)
                {
                    // If the autosave/backup failed, skip it and try for the next loaded project.
                    // Should only happen if FileInfo failed to initialize (most likely because of a file access violation)
#if DEBUG
                    Console.WriteLine(ex.Message);
#endif
                    //_Notifier.ShowError("Auto save failed.");
                    continue;
                }
            }
        }

        #endregion
        #region Compiling

        private void ExportCode()
        {
            try
            {
                propData.CommitEdit();
                if (TestError()) return;
                var saveFileDialog = new System.Windows.Forms.SaveFileDialog()
                {
                    InitialDirectory = (App.Current as App).SLDir,
                    Filter = "Lua Code|*.lua"
                };
                saveFileDialog.ShowDialog();
                if (string.IsNullOrEmpty(saveFileDialog.FileName)) return;
                (App.Current as App).SLDir = Path.GetDirectoryName(saveFileDialog.FileName);
                ActivatedWorkSpaceData.GatherCompileInfo(App.Current as App);
                ActivatedWorkSpaceData.SaveCode(saveFileDialog.FileName);
            }
            catch { }
        }

        private void ExportZip(bool run, TreeNode SCDebugger = null, TreeNode StageDebugger = null)
        {
            try
            {
                bool saveMeta = false;
                propData.CommitEdit();
                if (TestError()) return;
                GlobalCompileData.SCDebugger = SCDebugger;
                GlobalCompileData.StageDebugger = StageDebugger;

                App currentApp = Application.Current as App;

                if (!run)
                {
                    saveMeta = currentApp.SaveResMeta;
                    currentApp.SaveResMeta = false;
                }

                if (currentApp.DebugSaveProj) if (!SaveActiveFile()) return;

                DocumentData current = ActivatedWorkSpaceData;
                TxtLine.Text = "";
                if (CompileWorker.IsBusy) throw new InvalidOperationException();
                CompileWorker.RunWorkerAsync(new object[] { current, SCDebugger, StageDebugger, run, saveMeta });
                DebugString = "";
                tabOutput.IsSelected = true;
            }
            catch (EXEPathNotSetException)
            {

            }
            catch (InvalidRelativeResPathException)
            {

            }
            catch (InvalidOperationException)
            {

            }
        }

        private void BeginPackaging(object sender, DoWorkEventArgs args)
        {
            object[] arguments = args.Argument as object[];
            DocumentData current = arguments[0] as DocumentData;
            TreeNode SCDebugger = arguments[1] as TreeNode;
            TreeNode StageDebugger = arguments[2] as TreeNode;

            App currentApp = Application.Current as App;
            CompileProcess process = null;
            if (!(current is PlainDocumentData pdd && pdd.parentProj != null))
            {
                current.GatherCompileInfo(currentApp);
                current.CompileProcess.ProgressChanged +=
                    (o, e) => CompileWorker.ReportProgress(e.ProgressPercentage, e.UserState);
                current.CompileProcess.ExecuteProcess(SCDebugger != null, StageDebugger != null, App.Current as App);
                process = current.CompileProcess;
            }
            else
            {
                pdd.parentProj.GatherCompileInfo(currentApp);
                pdd.parentProj.CompileProcess.ProgressChanged +=
                    (o, e) => CompileWorker.ReportProgress(e.ProgressPercentage, e.UserState);
                pdd.parentProj.CompileProcess.ExecuteProcess(SCDebugger != null, StageDebugger != null, App.Current as App);
                process = pdd.parentProj.CompileProcess;
            }
            args.Result = new object[] { process, arguments[3], arguments[4] };
        }

        private void PackageProgressReport(object sender, ProgressChangedEventArgs args)
        {
            packagingLocked = true;
            DebugString += args.UserState?.ToString() + "\n";
            debugOutput.ScrollToEnd();
        }

        private void FinishPackaging(object sender, RunWorkerCompletedEventArgs args)
        {
            packagingLocked = false;
            object[] arguments = args.Result as object[];
            CompileProcess process = arguments[0] as CompileProcess;
            bool run = Convert.ToBoolean(arguments[1]);
            bool saveMeta = Convert.ToBoolean(arguments[2]);
            App currentApp = Application.Current as App;
            if (run)
            {
                RunLuaSTG(currentApp, process);
            }
            else
            {
                currentApp.SaveResMeta = saveMeta;
            }
        }

        private void RunLuaSTG(App currentApp, CompileProcess process)
        {
            PluginHandler.Plugin.Execution.BeforeRun(new ExecutionConfig()
            {
                ModName = process.projName
            });
            PluginHandler.Plugin.Execution.Run((s) =>
            {
                DebugString = s;
            }
            , () => App.Current.Dispatcher.Invoke(() => debugOutput.ScrollToEnd()));
        }

        #endregion
        #region Editor Events

        private void RaiseInsertStateChanged()
        {
            RaiseProertyChanged("IsBeforeState");
            RaiseProertyChanged("IsAfterState");
            RaiseProertyChanged("IsParentState");
            RaiseProertyChanged("IsChildState");
        }

        private void ButtonUP_Click(object sender, RoutedEventArgs e)
        {
            insertState = new BeforeFac();
        }

        private void ButtonDown_Click(object sender, RoutedEventArgs e)
        {
            insertState = new AfterFac();
        }

        private void ButtonChild_Click(object sender, RoutedEventArgs e)
        {
            insertState = new ChildFac();
        }

        private void ButtonParent_Click(object sender, RoutedEventArgs e)
        {
            insertState = new ParentFac();
        }

        private void WorkSpaceSelectedChanged(object sender, RoutedEventArgs e)
        {
            workSpace = sender as TreeView;
            SelectedNode = ((TreeNode)(workSpace.SelectedItem));
            if (selectedNode != null) this.propData.ItemsSource = selectedNode.attributes;
            // I really don't want this to crash. So fuck it: try/catch.
            string version = "LuaSTG Editor Sharp X v0.77.0";
            if (ActivatedWorkSpaceData != null)
            {
                Title = $"{version} - {ActivatedWorkSpaceData.RawDocName}";
                DiscordClient?.SetPresence(new RichPresence()
                {
                    Details = ActivatedWorkSpaceData.RawDocName,
                    State = "Editing Project",
                    Timestamps = new Timestamps(ActivatedWorkSpaceData.StartedTimestamp),
                    Assets = new Assets()
                    {
                        LargeImageKey = "sharpxicon",
                        SmallImageText = ActivatedWorkSpaceData.RawDocName,
                        SmallImageKey = "icon"
                    }
                });
            }
            else
            {
                Title = version;
                DiscordRpcReset();
            }
            //EditorConsole.Text = selectedNode.ToLua(0);
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            foreach (string s in InputWindowSelector.SelectComboBox(comboBox.Tag?.ToString()))
            {
                ComboBoxItem item = new ComboBoxItem() { Content = s };
                comboBox.Items.Add(item);
            }
            comboBox.Focus();
        }

        protected void UpdateLog_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Update Log.txt"));
            if (File.Exists(path))
            {
                Process log = new Process
                {
                    StartInfo = new ProcessStartInfo(path)
                };
                log.Start();
            }
        }

        protected void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            Sparkle.CheckForUpdatesAtUserRequest();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            propData.CommitEdit();
            while (Documents.Count > 0)
            {
                if (!CloseFile(Documents[0]))
                {
                    e.Cancel = true;
                    break;
                }
            }

            if (!e.Cancel)
            {
                //_Notifier?.ClearMessages(new ClearAll());
                //_Notifier?.Dispose();
                DiscordClient?.Dispose();
                App.Current.Shutdown();
            }
        }

        private void ButtonCloseFile_Click(object sender, RoutedEventArgs e)
        {
            propData.CommitEdit();
            Button btn = sender as Button;
            int btnHash = Convert.ToInt32(btn.Tag?.ToString());
            var toRemove = new List<DocumentData>();
            foreach (DocumentData wsd in Documents)
            {
                if (wsd.DocHash == btnHash) toRemove.Add(wsd);
            }

            DocumentData DocumentToRemove = toRemove[0];
            CloseFile(DocumentToRemove);
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) is TreeViewItem treeViewItem)
            {
                treeViewItem.Focus();
            }
        }

        private void TreeViewItem_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem
                && (sender as TreeViewItem).IsSelected)
            {
                propData.CommitEdit();
                AttrItem ai = selectedNode.GetRCInvoke();
                if (ai != null)
                {
                    IInputWindow iw = InputWindowSelector.SelectInputWindow(ai, ai.EditWindow, ai.AttrInput);
                    if (iw.ShowDialog() == true)
                    {
                        ActivatedWorkSpaceData.AddAndExecuteCommand(new EditAttrCommand(ai, ai.AttrInput, iw.Result));
                        var a = propData.ItemsSource;
                        propData.ItemsSource = null;
                        propData.ItemsSource = a;
                    }
                }
                //e.Handled = true;
            }
        }

        private void DataGrid_GotFocus(object sender, RoutedEventArgs e)
        {
            // Lookup for the source to be DataGridCell
            if (e.OriginalSource is DataGridCell dgc)
            {
                // Starts the Edit on the row;
                DataGrid grd = (DataGrid)sender;
                if (!dgc.IsReadOnly) grd.BeginEdit(e);
            }
        }

        private void EditorConsoleRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBase mb = (sender as DataGridRow).Tag as MessageBase;
            mb.Invoke();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowEditWindow();
        }

        private void TxtLine_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (ActivatedWorkSpaceData != null && int.TryParse(TxtLine.Text?.ToString(), out int i))
            {
                GotoLine(i);
            }
        }

        #endregion
        #region Attributes

        public void OpenAndFixNodeAttributes()
        {
            var loadFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                InitialDirectory = (App.Current as App).SLDir,
                Filter = "LuaSTG Sharp Editor File (*.lstges, *.lstgproj)|*.lstges;*.lstgproj",
                Multiselect = true
            };
            if (loadFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.Cancel) return;
            for (int i = 0; i < loadFileDialog.FileNames.Length; i++)
            {
                FixNodeAttributes(loadFileDialog.SafeFileNames[i], loadFileDialog.FileNames[i]);
                (App.Current as App).SLDir = Path.GetDirectoryName(loadFileDialog.FileNames[i]);
            }
        }

        public async void FixNodeAttributes(string name, string path)
        {
            try
            {
                DocumentData newDoc = DocumentData.GetNewByExtension(Path.GetExtension(path), Documents.MaxHash, name, path);
                Documents.AddAndAllocHash(newDoc);
                TreeNode t = await DocumentData.CreateNodeFromFileAsync(path, newDoc);
                newDoc.TreeNodes.Add(t);
                t.RaiseCreate(new OnCreateEventArgs() { parent = null });
                newDoc.OnOpening();
                //newDoc.TreeNodes[0].FixBan();
                newDoc.OriginalMeta.PropertyChanged += newDoc.OnEditing;

                newDoc.DocPath = "";
                Queue<TreeNode> nodes = new Queue<TreeNode>();
                nodes.Enqueue(newDoc.TreeNodes[0]);
                while (nodes.Count > 0)
                {
                    TreeNode n = nodes.Dequeue();
                    n.FixAttributesList();
                    foreach (TreeNode tn in n.Children)
                    {
                        nodes.Enqueue(tn);
                    }
                }
            }
            catch (JsonException e)
            {
                MessageBox.Show("Failed to open document or fix attribute. Please check whether the targeted file is in current version.\n"
                    + e.ToString()
                    , "LuaSTG Editor Sharp X", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
        #region Presets

        private void SavePreset()
        {
            TreeNode t = new RootFolder(null);
            TreeNode selected = SelectedNode.Clone() as TreeNode;
            t.AddChild(selected);
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Lua Presets|*.lstgpreset",
            };
            dialog.InitialDirectory = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                , "LuaSTG Editor Sharp X Presets"));
            string path = "";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                path = dialog.FileName;
                FileStream s = null;
                StreamWriter sw = null;
                try
                {
                    s = new FileStream(path, FileMode.Create, FileAccess.Write);
                    sw = new StreamWriter(s, Encoding.UTF8);
                    t.SerializeFile(sw, 0);
                    GetPresets();
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Unable to write to file \"{path}\".\n{e}", "LuaSTG Editor Sharp X"
                        , MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (sw != null) sw.Close();
                    if (s != null) s.Close();
                }
            }
        }

        private async Task InsertPreset(string s)
        {
            if (Directory.Exists(s))
            {
                var dialog = new SingleLineInput("") { Title = "Input Directory Name" };
                if (dialog.ShowDialog() == true)
                {
                    Directory.CreateDirectory(Path.GetFullPath(Path.Combine(s, dialog.Result)));
                    GetPresets();
                }
            }
            else if (Path.GetExtension(s) == ".lstgpreset")
            {
                try
                {
                    TreeNode t = await DocumentData.CreateNodeFromFileAsync(s, ActivatedWorkSpaceData);
                    if (t.Children == null || t.Children.Count < 1 || t.Children[0] == null) throw new Exception();
                    Insert(t.Children[0], false);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Failed to load preset.\n{e}");
                }
            }
        }

        #endregion
        #region Commands
        #region Can Execute

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (ActivatedWorkSpaceData != null)
            {
                e.CanExecute = ActivatedWorkSpaceData.IsUnsaved;
            }
            else e.CanExecute = false;
        }

        private void SaveAsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null;
        }

        private void CloseCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null;
        }

        private void UndoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (ActivatedWorkSpaceData != null)
            {
                e.CanExecute = ActivatedWorkSpaceData.commandFlow.Count > 0;
            }
            else e.CanExecute = false;
        }

        private void RedoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (ActivatedWorkSpaceData != null)
            {
                e.CanExecute = ActivatedWorkSpaceData.undoFlow.Count > 0;
            }
            else e.CanExecute = false;
        }

        private void CutCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (selectedNode != null)
            {
                e.CanExecute = selectedNode.CanLogicallyDelete();
            }
            else e.CanExecute = false;
        }

        private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void PasteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null && clipBoard != null;
        }

        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (selectedNode != null)
            {
                e.CanExecute = selectedNode.CanLogicallyDelete();
            }
            else e.CanExecute = false;
        }

        private void FoldTreeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void UnfoldTreeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void FoldRegionCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode as Region != null;
            // && selectedNode.Parent.ValidateChild(new Folder(ActivatedWorkSpaceData));
        }

        private void UnfoldAsRegionCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null && (selectedNode is Folder ||
                                                    selectedNode is FolderRed ||
                                                    selectedNode is FolderGreen ||
                                                    selectedNode is FolderBlue ||
                                                    selectedNode is FolderYellow);
        }

        private void SwitchBanCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null && selectedNode.CanLogicallyBeBanned();
        }

        private void GoToLineXCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null && int.TryParse(e.Parameter?.ToString(), out int i);
        }

        private void GoToDefCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode?.GetReferredTreeNode() != null;
        }

        private void ViewCodeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void ExportCodeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null;
        }

        private void ExportZipCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null && !packagingLocked && (App.Current as App).IsEXEPathSet;
        }

        private void RunProjectCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null && !packagingLocked && (App.Current as App).IsEXEPathSet;
        }

        private void SCDebugCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            if (selectedNode == null) return;
            TreeNode t = selectedNode;
            while (t != null)
            {
                if (PluginHandler.Plugin.MatchBossSCNodeTypes(t.GetType()))
                {
                    e.CanExecute = true;
                }
                t = t.Parent;
            }
            e.CanExecute = e.CanExecute && !packagingLocked && (App.Current as App).IsEXEPathSet;
        }

        private void StageDebugCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            if (selectedNode == null) return;
            TreeNode t = selectedNode;
            while (t != null && (t?.Parent is Folder ||
                                 t?.Parent is FolderRed ||
                                 t?.Parent is FolderGreen ||
                                 t?.Parent is FolderBlue ||
                                 t?.Parent is FolderYellow))
            {
                t = t.Parent;
            }
            e.CanExecute = PluginHandler.Plugin.MatchStageNodeTypes(t?.Parent?.Parent?.GetType()) && !packagingLocked
                && (App.Current as App).IsEXEPathSet;
        }

        private void InsertPresetCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void SavePresetCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        private void ViewFileFolderCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null;
        }

        private void ViewModFolderCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string s = (App.Current as App).LuaSTGExecutablePath;
            e.CanExecute = File.Exists(s) && Directory.Exists(Path.Combine(Path.GetDirectoryName(s), "mod"));
        }

        private void ViewDefinitionCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = ActivatedWorkSpaceData != null;
        }

        private void EditNodeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = selectedNode != null;
        }

        #endregion
        #region Executed

        private void NewCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            NewDoc();
        }

        private void OpenCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            OpenDoc();
        }

        private void SaveCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SaveDoc(ActivatedWorkSpaceData);
        }

        private void SaveAsCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SaveActiveFileAs();
        }

        private void CloseCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            CloseFile(ActivatedWorkSpaceData);
        }

        private void UndoCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Undo();
            var a = propData.ItemsSource;
            propData.ItemsSource = null;
            propData.ItemsSource = a;
        }

        private void RedoCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Redo();
            var a = propData.ItemsSource;
            propData.ItemsSource = null;
            propData.ItemsSource = a;
        }

        private void CutCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            CutNode();
        }

        private void CopyCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            CopyNode();
        }

        private void PasteCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            PasteNode();
        }

        private void DeleteCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            DeleteNode();
        }

        private void FoldTreeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            selectedNode.FoldTree();
        }

        private void UnfoldTreeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            selectedNode.ExpandTree();
        }

        private void FoldRegionCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            FoldRegion();
        }

        private void UnfoldAsRegionCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            UnfoldAsRegion();
        }
        private void SwitchBanCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            selectedNode.IsBanned_InvokeCommand = !selectedNode.IsBanned;
        }

        private void GoToLineXCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            int.TryParse(e.Parameter?.ToString(), out int line);
            GotoLine(line);
        }

        private void GoToDefCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TreeNode t = selectedNode?.GetReferredTreeNode();
            if (t != null) Reveal(t);
        }

        private void FixAttributeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            OpenAndFixNodeAttributes();
        }

        private void LibraryToolsCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            PluginTool pt = (e.Parameter as PluginTool);
            PluginToolResult ptr = pt.ExecutePlugin(new PluginToolParameter(SelectedNode));
            clipBoard = ptr.clipBoard;
            if (ptr.newDocument != null) Documents.AddAndAllocHash(ptr.newDocument);
            foreach (Command c in ptr.commands)
            {
                ActivatedWorkSpaceData.AddAndExecuteCommand(c);
            }
        }

        private void ViewCodeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ViewCode();
        }

        private void ExportCodeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ExportCode();
        }

        private void ExportZipCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ExportZip(false);
        }

        private void RunProjectCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ExportZip(true);
        }

        private void SCDebugCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            TreeNode t = selectedNode;
            while (t != null)
            {
                if (PluginHandler.Plugin.MatchBossSCNodeTypes(t.GetType()))
                {
                    break;
                }
                t = t.Parent;
            }
            ExportZip(true, t, null);
        }

        private void StageDebugCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ExportZip(true, null, selectedNode);
        }

        private async void InsertPresetCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            string s = e.Parameter?.ToString();
            await InsertPreset(s);
        }

        private void SavePresetCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            SavePreset();
        }

        private void RefreshPresetCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            GetPresets();
        }

        private void SwitchBeforeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            insertState = new BeforeFac();
            RaiseInsertStateChanged();
        }

        private void SwitchAfterCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            insertState = new AfterFac();
            RaiseInsertStateChanged();
        }

        private void SwitchChildCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            insertState = new ChildFac();
            RaiseInsertStateChanged();
        }

        private void SwitchParentCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            insertState = new ParentFac();
            RaiseInsertStateChanged();
        }

        private void ViewFileFolderCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process folder = new Process
                {
                    StartInfo = new ProcessStartInfo(Path.GetDirectoryName(ActivatedWorkSpaceData.DocPath))
                    {
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                folder.Start();
            }
            catch { }
        }

        private void ViewModFolderCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process folder = new Process
                {
                    StartInfo = new ProcessStartInfo(Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName((App.Current as App).LuaSTGExecutablePath), "mod")))
                    {
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                folder.Start();
            }
            catch { }
        }

        private void ViewDefinitionCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            propData.CommitEdit();
            PluginHandler.Plugin.GetViewDefinitionWindow(ActivatedWorkSpaceData).ShowDialog();
        }

        private void SettingsCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (int.TryParse(e.Parameter?.ToString(), out int i))
            {
                new SettingsWindow(i).ShowDialog();
            }
            else
            {
                new SettingsWindow().ShowDialog();
            }
        }

        private void AboutNodeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            new AboutNodeWindow().ShowDialog();
        }

        private void AdjustPropCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            propData.CommitEdit();
            var button = sender as Button;
            //AttrItem ai = button.GetBindingExpression(TagProperty).ResolvedSource as AttrItem;
            AttrItem ai = button.Tag as AttrItem;
            //try
            {
                IInputWindow iw = InputWindowSelector.SelectInputWindow(ai, e.Parameter?.ToString(), ai.AttrInput);
                if (iw.ShowDialog() == true)
                {
                    ActivatedWorkSpaceData.AddAndExecuteCommand(new EditAttrCommand(ai, ai.AttrInput, iw.Result));
                    var a = propData.ItemsSource;
                    propData.ItemsSource = null;
                    propData.ItemsSource = a;
                }
            }
            //catch { }
        }

        private void OpenMarketplaceExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (int.TryParse(e.Parameter?.ToString(), out int i))
            {
                new AddonsWindow(i).ShowDialog();
            }
            else
            {
                new AddonsWindow().ShowDialog();
            }
        }

        private void EditNodeCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ShowEditWindow();
        }

        #endregion
        #endregion
        #region Obsolete code for some reason

        [Obsolete]
        private void SaveXML()
        {
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("xsi", @"http://www.w3.org/2001/XMLSchema-instance");
            namespaces.Add("m", "LuaSTG/Generic");
            XmlSerializer serializer =
                new XmlSerializer(typeof(RootFolder)
                , PluginHandler.Plugin.NodeTypeCache.NodeTypes.ToArray());
            StreamWriter sw = new StreamWriter(
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LuaSTG Editor/xmltest.xml")));
            serializer.Serialize(sw, ActivatedWorkSpaceData.TreeNodes[0], namespaces);
            sw.Close();
            MessageBox.Show("");

            XmlReaderSettings readerSettings = new XmlReaderSettings();

            var sr = new StreamReader(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LuaSTG Editor/xmltest.xml")));
            DocumentData newDoc = DocumentData.GetNewByExtension(".lstges", Documents.MaxHash, "xmltest", Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LuaSTG Editor/xmltest.xml")));
            Documents.AddAndAllocHash(newDoc);
            TreeNode t = serializer.Deserialize(sr) as TreeNode;
            newDoc.TreeNodes.Add(t);
            newDoc.OnOpening();
            //newDoc.TreeNodes[0].FixBan();
            newDoc.OriginalMeta.PropertyChanged += newDoc.OnEditing;
            sr.Close();
        }

        #endregion
        #region ProertyChanged (lol)

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaiseProertyChanged([CallerMemberName] string propName = default)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #endregion
    }
}
