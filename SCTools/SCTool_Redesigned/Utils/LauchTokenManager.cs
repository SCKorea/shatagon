using System.IO;

namespace SCTool_Redesigned.Utils
{
    class LauchTokenManager
    {
        private static LauchTokenManager _instance = null;
        private static readonly object _padlock = new object();

        private FileSystemWatcher _watcher = null;
        private string _srcpath, _dstpath;              //source: (game)+\\+ loginData.json dest: localappdata + loginData.json
        private string _tokenName = "loginData.json";
        private System.DateTime _lastevent = System.DateTime.MinValue;

        LauchTokenManager()
        { }
        public static LauchTokenManager Instance
        {
            get
            {
                lock(_padlock)      //make thread-safe
                {
                    if(_instance == null)
                    {
                        _instance = new LauchTokenManager();
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
            if (_watcher != null)    return;
            _watcher = new FileSystemWatcher(_srcpath);

            _watcher.NotifyFilter = NotifyFilters.LastWrite
                                  | NotifyFilters.CreationTime
                                  | NotifyFilters.LastAccess;
            _watcher.EnableRaisingEvents = true;
            _watcher.Filter = _tokenName;

            _watcher.Changed += new FileSystemEventHandler(UpdateToken);

            NLog.LogManager.GetCurrentClassLogger().Info("Watcher started at "+_srcpath);
        }
        private void UpdateToken(object sender, FileSystemEventArgs e)
        {
            if ( File.GetLastWriteTime(_srcpath + "\\" + _tokenName).Subtract(_lastevent).Minutes < 1)
                return; //discard duplicated events

            File.Copy(_srcpath + "\\" + _tokenName, _dstpath + _tokenName, true);
            NLog.LogManager.GetCurrentClassLogger().Info("Token Copied");

            _lastevent = File.GetLastWriteTime(_srcpath + "\\" + _tokenName);
            //FIXME: COPY시 예외들 처리
        }
        public bool LoadToken()
        {
            if(_watcher == null)
                throw new System.Exception(Properties.Resources.MSG_Desc_InvalidAccess);

            try
            {
                _watcher.EnableRaisingEvents = false;
                File.Copy(_dstpath + _tokenName, _srcpath + "\\" + _tokenName, true);
            }
            catch(FileNotFoundException err)
            {
                throw new FileNotFoundException(Properties.Resources.MSG_Desc_NeedTokenGenerate); //need token to generate
            }
            catch(IOException copyErr)
            {
                return false;
            }
            _watcher.EnableRaisingEvents = true;

            NLog.LogManager.GetCurrentClassLogger().Info("Token Loaded");
            return true;
        }
    }
}
