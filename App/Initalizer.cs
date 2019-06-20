using System;
using System.IO;
using System.Windows.Forms;
using App.Interfaces;
using App.Misc;
using App.Models;
using App.Models.Forms;
using App.Presenters.Forms;
using App.Properties;
using CollectionManager.DataTypes;
using CollectionManager.Modules.CollectionsManager;
using CollectionManager.Modules.FileIO;
using CollectionManagerExtensionsDll.Modules.API.osustats;
using CollectionManagerExtensionsDll.Utils;
using Common;
using GuiComponents.Interfaces;

namespace App
{
    public class Initalizer : ApplicationContext
    {
        public static OsuFileIo OsuFileIo = new OsuFileIo(new BeatmapExtension());
        public static CollectionsManagerWithCounts CollectionsManager;
        public static Beatmaps LoadedBeatmaps => OsuFileIo.LoadedMaps.Beatmaps;
        public static Collections LoadedCollections => CollectionsManager.LoadedCollections;
        public static string OsuDirectory;
        public static CollectionEditor CollectionEditor { get; private set; }
        private IUserDialogs UserDialogs { get; set; }// = new GuiComponents.UserDialogs();
        public static OsuStatsApi WebCollectionProvider = new OsuStatsApi("", OsuFileIo.LoadedMaps);
        public void Run(string[] args)
        {
            //IUserDialogs can be implemented in WinForm or WPF or Gtk or Console or...?
            UserDialogs = GuiComponentsProvider.Instance.GetClassImplementing<IUserDialogs>();

            if (Settings.Default.DontAskAboutOsuDirectory)
                OsuDirectory = OsuFileIo.OsuPathResolver.GetOsuDir(null, UserDialogs.SelectDirectory);
            else
                OsuDirectory = OsuFileIo.OsuPathResolver.GetOsuDir(dir =>
                {
                    var result = UserDialogs.YesNoMessageBox($"Detected osu! in \"{dir}\"{Environment.NewLine}Is that correct?", "osu! directory", MessageBoxType.Question,
                        "Don't ask me again");
                    Settings.Default.DontAskAboutOsuDirectory = result.doNotAskAgain;
                    Settings.Default.Save();
                    return result.Result;
                }, UserDialogs.SelectDirectory);

            if (OsuDirectory == string.Empty)
            {
                UserDialogs.OkMessageBox("Valid osu! directory is required to run Collection Manager" + Environment.NewLine + "Exiting...", "Error", MessageBoxType.Error);
                Quit();
            }

            //Load osu database and setting files
            var osuDbFile = Path.Combine(OsuDirectory, @"osu!.db");
            OsuFileIo.OsuDatabase.Load(osuDbFile);
            OsuFileIo.OsuSettings.Load(OsuDirectory);
            BeatmapUtils.OsuSongsDirectory = OsuFileIo.OsuSettings.CustomBeatmapDirectoryLocation;

            //Init "main" classes
            CollectionsManager = new CollectionsManagerWithCounts(LoadedBeatmaps);

            var collectionAddRemoveForm = GuiComponentsProvider.Instance.GetClassImplementing<ICollectionAddRenameForm>();
            CollectionEditor = new CollectionEditor(CollectionsManager, CollectionsManager, collectionAddRemoveForm, OsuFileIo.LoadedMaps);

            if (args.Length > 0)
            {
                if (File.Exists(args[0]))
                {
                    CollectionsManager.EditCollection(CollectionEditArgs.AddCollections(OsuFileIo.CollectionLoader.LoadCollection(args[0])));
                }
            }

            var updateChecker = new UpdateChecker();
            updateChecker.CheckForUpdates();
            var infoTextModel = new InfoTextModel(updateChecker);

            var mainForm = GuiComponentsProvider.Instance.GetClassImplementing<IMainFormView>();
            var mainPresenter = new MainFormPresenter(mainForm, new MainFormModel(CollectionEditor, UserDialogs), infoTextModel, WebCollectionProvider);

            //set initial text info and update events
            SetTextData(infoTextModel);


            var loginForm = GuiComponentsProvider.Instance.GetClassImplementing<ILoginFormView>();
            var guiActionsHandler = new GuiActionsHandler(OsuFileIo, CollectionsManager, UserDialogs, mainForm, mainPresenter, loginForm);

            if (!string.IsNullOrWhiteSpace(Settings.Default.Osustats_apiKey))
                guiActionsHandler.SidePanelActionsHandler.OsustatsLogin(null, Settings.Default.Osustats_apiKey);

            mainForm.ShowAndBlock();
            Quit();
        }

        private void SetTextData(IInfoTextModel model)
        {
            model.SetBeatmapCount(LoadedBeatmaps.Count);
            CollectionsManager.LoadedCollections.CollectionChanged += (s, a) =>
            {
                model.SetCollectionCount(CollectionsManager.CollectionsCount, CollectionsManager.BeatmapsInCollectionsCount);
                model.SetMissingMapSetsCount(CollectionsManager.MissingMapSetsCount);
            };
            LoadedBeatmaps.CollectionChanged += (s, a) =>
            {
                model.SetBeatmapCount(LoadedBeatmaps.Count);
            };
        }


        private static void Quit()
        {
            Settings.Default.Save();

            if (System.Windows.Forms.Application.MessageLoop)
            {
                System.Windows.Forms.Application.Exit();
            }
            else
            {
                System.Environment.Exit(1);
            }
        }
    }
}