using System.IO;

namespace SCTool_Redesigned.Utils
{
    class LaunchTokenManager
    {
        private static LaunchTokenManager _instance = null;
        private static readonly object _padlock = new object();

        private FileSystemWatcher _watcher = null;
        private string _srcpath, _dstpath;              //source: (game)+\\+ loginData.json dest: localappdata + loginData.json
        private string _tokenName = "loginData.json";
        private System.DateTime _lastevent = System.DateTime.MinValue;

        LaunchTokenManager()
        { }
        public static LaunchTokenManager Instance
        {
            get
            {
                lock (_padlock)      //make thread-safe
                {
                    if (_instance == null)
                    {
                        _instance = new LaunchTokenManager();
                    }
                    return _instance;
                }
            }
        }

        public void UpdateLauchTokenManager(string srcpath, string destpath)
        {
            _srcpath = srcpath;
            _dstpath = destpath;
        }

        public void BeginWatch()
        {
            if (_watcher != null)
                return;
            _watcher = new FileSystemWatcher(_srcpath);

            _watcher.NotifyFilter = NotifyFilters.LastWrite
                                  | NotifyFilters.CreationTime
                                  | NotifyFilters.FileName;
            _watcher.EnableRaisingEvents = true;
            _watcher.Filter = _tokenName;

            _watcher.Changed += new FileSystemEventHandler(UpdateToken);
            _watcher.Created += new FileSystemEventHandler(UpdateToken);

            NLog.LogManager.GetCurrentClassLogger().Info("Watcher started at " + _srcpath);
        }
        private void UpdateToken(object sender, FileSystemEventArgs e)
        {
            string _tokenpath = _srcpath + "\\" + _tokenName;
            NLog.LogManager.GetCurrentClassLogger().Info("Watcher awake");
            if (new FileInfo(_tokenpath).Length < 6)
            {
                NLog.LogManager.GetCurrentClassLogger().Info("but was empty event");
                return; //discard empty file events
            }
            if (File.GetLastWriteTime(_tokenpath).Subtract(_lastevent).Seconds < 2)
            {
                NLog.LogManager.GetCurrentClassLogger().Info("but fall by " + File.GetLastWriteTime(_tokenpath).Subtract(_lastevent).Seconds.ToString());
                return; //discard duplicated events
            }

            File.Copy(_tokenpath, _dstpath + _tokenName, true);
            NLog.LogManager.GetCurrentClassLogger().Info("Token Copied");

            _lastevent = File.GetLastWriteTime(_tokenpath);
            //FIXME: COPY시 예외들 처리
        }
        public bool LoadToken()
        {
            if (_watcher == null)
                throw new System.Exception(Properties.Resources.MSG_Desc_InvalidAccess);

            try
            {
                if (new FileInfo(_dstpath + _tokenName).Length < 6)
                { throw new FileNotFoundException("file is empty"); }

                _watcher.EnableRaisingEvents = false;
                File.Copy(_dstpath + _tokenName, _srcpath + "\\" + _tokenName, true);
            }
            catch (FileNotFoundException err)
            {
                NLog.LogManager.GetCurrentClassLogger().Error(err.Message);
                throw new FileNotFoundException(Properties.Resources.MSG_Desc_NeedTokenGenerate); //need token to generate
            }
            catch
            {
                return false;
            }
            finally
            {
                _watcher.EnableRaisingEvents = true;
            }

            NLog.LogManager.GetCurrentClassLogger().Info("Token Loaded");
            return true;
        }
        public void UnloadToken()
        {
            try
            {
                File.Delete(_srcpath + "\\" + _tokenName);
            }
            catch (FileNotFoundException err)
            {
                throw new FileNotFoundException(Properties.Resources.MSG_Desc_InvalidAccess + err.Message);
            }

            NLog.LogManager.GetCurrentClassLogger().Info("Token Unloaded");
        }
    }
}
