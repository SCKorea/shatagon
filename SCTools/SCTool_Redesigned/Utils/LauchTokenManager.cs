using System.IO;

namespace SCTool_Redesigned.Utils
{
    class LauchTokenManager
    {
        private static LauchTokenManager instance = null;
        private static readonly object padlock = new object();

        private FileSystemWatcher _watcher = null;
        private string _srcpath, _dstpath;              //source: (game)+\\+ loginData.json dest: localappdata + loginData.json
        private string _tokenName = "loginData.json";

        LauchTokenManager()
        { }
        public static LauchTokenManager Instance
        {
            get
            {
                lock(padlock)
                {
                    if(instance == null)
                    {
                        instance = new LauchTokenManager();
                    }
                    return instance;
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
            _watcher = new FileSystemWatcher(_srcpath);  //<==path

            _watcher.NotifyFilter = NotifyFilters.CreationTime
                                  | NotifyFilters.LastWrite
                                  | NotifyFilters.LastAccess;
            _watcher.EnableRaisingEvents = true;
            _watcher.Filter = _tokenName;

            _watcher.Created += new FileSystemEventHandler(UpdateToken);
            _watcher.Changed += new FileSystemEventHandler(UpdateToken);

            NLog.LogManager.GetCurrentClassLogger().Info("Watcher started at "+_srcpath);
        }
        private void UpdateToken(object sender, FileSystemEventArgs e)
        {
            NLog.LogManager.GetCurrentClassLogger().Info("Triggered Action: "+e.ToString());
            if (e.ChangeType == WatcherChangeTypes.Deleted)
                return;
            NLog.LogManager.GetCurrentClassLogger().Info("Update token!");
            File.Copy(_srcpath + "\\" + _tokenName, _dstpath + _tokenName, true);
            NLog.LogManager.GetCurrentClassLogger().Info("Copied token!");
            //이미 있는데 1분안에 갱신된 거라면 복사하지 않는다. src->dest <- watcher가 있는데 어차피 안해도 될듯
            //FIXME: COPY시 예외들 처리
            //FIXME: 런칭시 트리거되는 이유?
        }
        public bool LoadToken()
        {
            try
            {
                File.Copy(_dstpath + _tokenName, _srcpath + "\\" + _tokenName, true);
            }
            catch(IOException copyErr)
            {
                return false;
            }
            return true;
        }
    }
}
