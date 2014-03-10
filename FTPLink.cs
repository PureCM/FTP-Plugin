using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Xml;
using System.Net;
using System.IO;
using EnterpriseDT.Net.Ftp;
using EnterpriseDT.Net.Ssh;
//using EnterpriseDT.Util.Debug;


using PureCM.Client;

namespace Plugin_FTP
{
    class FTPLink
    {
        private FTPPlugin m_oPlugin;
        private String m_strReposName;
        private String m_strStreamName;
        private String m_strWorkspacePath;
        private String m_strFTPPath;
        private String m_strFTPName;
        private String m_strFTPType;
        private int m_nFTPPort;
        private String m_strFTPUsername;
        private String m_strFTPPassword;
        SecureFTPConnection m_oSFTPConn;
        private String m_strFTPFullPath;
        private bool m_bReSyncOnStartup = false;
        private bool m_bPassive = false;

        internal String ReposName
        {
            get { return m_strReposName; }
        }

        internal String WorkspacePath
        {
            get { return m_strWorkspacePath; }
        }

        internal bool ReSyncOnStartup
        {
            get { return m_bReSyncOnStartup; }
        }

        internal FTPLink(
            FTPPlugin oPlugin,
            string strReposName,
            string strStreamName,
            string strWorkspacePath,
            string strFTPPath,
            string strFTPName,
            string strFTPType,
            int nServerPort,
            string strFTPUsername,
            string strFTPPassword,
            bool bPassive,
            bool bReSyncOnStartup)
        {
            m_oPlugin = oPlugin;
            m_strReposName = strReposName;
            m_strStreamName = strStreamName;
            m_strWorkspacePath = strWorkspacePath;
            m_strFTPPath = strFTPPath.Replace('\\', '/');
            m_strFTPName = strFTPName;
            m_strFTPType = strFTPType;
            m_nFTPPort = nServerPort;
            m_strFTPUsername = strFTPUsername;
            m_strFTPPassword = strFTPPassword;
            m_strFTPFullPath = String.Empty;
            m_bPassive = bPassive;
            m_bReSyncOnStartup = bReSyncOnStartup;
        }

        internal bool Connect()
        {
            bool bRet = true;

            try
            {
                m_oSFTPConn = new SecureFTPConnection();

                m_oSFTPConn.LicenseOwner = "PureCMcomLtd";
                m_oSFTPConn.LicenseKey = "075-6658-5840-7862";
                m_oSFTPConn.ServerAddress = m_strFTPName;
                m_oSFTPConn.ServerPort = m_nFTPPort;
                m_oSFTPConn.UserName = m_strFTPUsername;
                m_oSFTPConn.Password = m_strFTPPassword;
                m_oSFTPConn.MultiTransferSleepEnabled = true;
                m_oSFTPConn.MultiTransferSleepTime = 10;
                m_oSFTPConn.KeepAliveTransfer = true;
                m_oSFTPConn.TransferType = FTPTransferType.BINARY;

                if (m_strFTPType == "sftp")
                {
                    m_oPlugin.LogInfo("Using sftp");
                    m_oSFTPConn.Protocol = FileTransferProtocol.SFTP;
                    m_oSFTPConn.AuthenticationMethod = AuthenticationType.Password;
                    m_oSFTPConn.ServerValidation = SecureFTPServerValidationType.None;
                }
                else
                {
                    if (m_bPassive)
                    {
                        m_oPlugin.LogInfo("Using passive ftp");
                        m_oSFTPConn.ConnectMode = FTPConnectMode.PASV;
                    }
                    else
                    {
                        m_oPlugin.LogInfo("Using active ftp");
                        m_oSFTPConn.ConnectMode = FTPConnectMode.ACTIVE;
                    }
                }

                m_oPlugin.LogInfo("Connecting to " + m_strFTPType + " server '" + m_strFTPName + "' on port '" + m_nFTPPort + "'.");

                m_oSFTPConn.Connect();
                m_strFTPFullPath = m_oSFTPConn.ServerDirectory;
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to connect to " + m_strFTPType + " server '" + m_strFTPName + "'. " + e.Message);
                m_oSFTPConn = null;
            }

            return bRet;
        }

        internal bool Disconnect()
        {
            bool bRet = true;

            if (m_oSFTPConn != null)
            {
                m_oPlugin.LogInfo("Disconnecting from " + m_strFTPType + " server '" + m_strFTPName + "'.");

                try
                {
                    m_oSFTPConn.Close();
                    m_oSFTPConn = null;
                    m_strFTPFullPath = String.Empty;
                }
                catch (Exception e)
                {
                    bRet = false;
                    m_oPlugin.LogError("Failed to disconnect from " + m_strFTPType + " server '" + m_strFTPName + "' (" + e.Message + ").");
                }
            }

            return bRet;
        }

        internal bool Validate(Repository oRepos)
        {
            bool bRet = false;

            if (Connect())
            {
                if (CheckFTPFolderExists(m_strFTPPath))
                {
                    m_oPlugin.LogInfo("Ftp folder '" + m_strFTPPath + "' exists");

                    Workspace oWS = FindWorkspace(oRepos, false);

                    if (oWS == null)
                    {
                        m_oPlugin.LogInfo("The workspace '" + m_strWorkspacePath + "' has not yet been created.");
                    }

                    bRet = true;
                }
                else
                {
                    m_oPlugin.LogError("The folder '" + m_strFTPPath + "' does not exist on '" + m_strFTPName + "'.");
                }
            }

            bRet = Disconnect() && bRet;

            return bRet;
        }

        internal bool UploadWorkspaceChanges(Workspace oWorkspace)
        {
            Changeset oLastChangeset = oWorkspace.IntegratedChangesets.GetLast();
            SDK.TPCMReturnCode tRet;

            oWorkspace.UpdateToLatest( out tRet );

            if (tRet != SDK.TPCMReturnCode.pcmSuccess)
            {
                m_oPlugin.LogError("Failed to update workspace '" + oWorkspace.Path + "'.");
                return false;
            }

            Changesets oChangesets = oWorkspace.IntegratedChangesets;

            if (oChangesets.GetLast().Id <= oLastChangeset.Id)
            {
                oChangesets.Dispose();
                m_oPlugin.LogInfo("No new changesets to upload for workspace '" + oWorkspace.Path + "'. '" + oLastChangeset.IdString + "' is the last submitted changeset.");
                return true;
            }

            FTPFileActions oActions = new FTPFileActions();

            foreach (Changeset oCurrentChangeset in oChangesets)
            {
                if (oCurrentChangeset.Id > oLastChangeset.Id)
                {
                    m_oPlugin.LogInfo("Processing Changeset '" + oCurrentChangeset.IdString + "'...");
                    ChangeItems oChangeItems = oCurrentChangeset.Items;

                    foreach (ChangeItem oChangeItem in oChangeItems)
                    {
                        switch (oChangeItem.Type)
                        {
                            case SDK.TPCMChangeItemType.pcmAdd:
                            case SDK.TPCMChangeItemType.pcmEdit:
                                {
                                    if (oChangeItem.RenamePath.Length > 0)
                                    {
                                        oActions.DeleteFile(oChangeItem.Path);
                                        oActions.UploadFile(oChangeItem.RenamePath);
                                    }
                                    else
                                    {
                                        oActions.UploadFile(oChangeItem.Path);
                                    }
                                }
                                break;
                            case SDK.TPCMChangeItemType.pcmDelete:
                                oActions.DeleteFile(oChangeItem.Path);
                                break;
                            case SDK.TPCMChangeItemType.pcmAddFolder:
                                oActions.UploadFolder(oChangeItem.Path);
                                break;
                            case SDK.TPCMChangeItemType.pcmDeleteFolder:
                                oActions.DeleteFolder(oChangeItem.Path);
                                break;
                        }
                    }

                }
            }

            oChangesets.Dispose();

            foreach (FTPFileAction oAction in oActions.m_aoActions)
            {
                switch (oAction.m_tAction)
                {
                    case FTPFileAction.TFTPAction.tUploadFile:
                        UploadFile(oAction.FilePath);
                        break;
                    case FTPFileAction.TFTPAction.tDeleteFile:
                        DeleteFile(oAction.FilePath);
                        break;
                    case FTPFileAction.TFTPAction.tUploadFolder:
                        UploadDir(oAction.FilePath);
                        break;
                    case FTPFileAction.TFTPAction.tDeleteFolder:
                        DeleteDir(oAction.FilePath);
                        break;
                }
            }

            m_oPlugin.LogInfo(oActions.Print());

            return true;
        }

        internal Repository FindRepos(Repositories oRepositories)
        {
            return oRepositories.ByName(m_strReposName);
        }

        internal bool Compare(Repository oRepos, PureCM.Client.Stream oStream)
        {
            return (oRepos.Name == m_strReposName) && (oStream.StreamPath == m_strStreamName);
        }

        internal bool UploadRootDir()
        {
            bool bRet = true;

            m_oPlugin.LogInfo("Uploading all workspace files from '" + m_strWorkspacePath + "'.");

            FTPFileActions oActions = new FTPFileActions();

            foreach (string strDir in Directory.GetDirectories(m_strWorkspacePath))
            {
                string strName = Path.GetFileName(strDir);

                if ( ( strName != "" ) && (strName != "_purecm") && (strName != ".purecm"))
                {
                    oActions.UploadFolder(strName);
                }
            }

            foreach (string strFile in Directory.GetFiles(m_strWorkspacePath))
            {
                string strName = Path.GetFileName(strFile);

                if ( ( strName != "" ) )
                {
                    oActions.UploadFile(strName);
                }
            }

            foreach (FTPFileAction oAction in oActions.m_aoActions)
            {
                switch (oAction.m_tAction)
                {
                    case FTPFileAction.TFTPAction.tUploadFile:
                        UploadFile(oAction.FilePath);
                        break;
                    case FTPFileAction.TFTPAction.tUploadFolder:
                        UploadDir(oAction.FilePath);
                        break;
                }
            }

            return bRet;
        }

        internal bool UploadDir(string strDirPath)
        {
            if (!strDirPath.StartsWith("\\"))
            {
                strDirPath = "\\" + strDirPath;
            }

            bool bRet = true;
            string strLocalPath = m_strWorkspacePath + strDirPath;
            Plugin_FTP.FilePath oRemotePath = new Plugin_FTP.FilePath(m_strFTPFullPath + "/" + m_strFTPPath + strDirPath);

            m_oPlugin.LogInfo("Uploading folder '" + strLocalPath + " to '" + oRemotePath + "'.");

            try
            {
                string strParentPath = oRemotePath.ParentPath;

                if (m_oSFTPConn.ChangeWorkingDirectory(strParentPath))
                {
                    m_oSFTPConn.UploadDirectory(strLocalPath, oRemotePath.Name);
                }
                else
                {
                    bRet = false;
                    m_oPlugin.LogError("Failed to access remote directory '" + strParentPath + "'.");
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to upload folder '" + strLocalPath + "' to '" + oRemotePath + "'. " + e.Message);
            }

            return bRet;
        }

        private bool CreateFolder(string strDirPath)
        {
            bool bRet = true;
            Plugin_FTP.FilePath oRemotePath = new Plugin_FTP.FilePath(m_strFTPFullPath + "/" + m_strFTPPath + strDirPath);

            m_oPlugin.LogInfo("Creating folder '" + oRemotePath + "'.");

            try
            {
                string strParentPath = oRemotePath.ParentPath;

                if (m_oSFTPConn.ChangeWorkingDirectory(strParentPath))
                {
                    m_oSFTPConn.CreateDirectory(oRemotePath.Name);
                }
                else
                {
                    bRet = false;
                    m_oPlugin.LogInfo("Failed to access remote directory '" + strParentPath + "'.");
                }
                
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogInfo("Failed to create folder '" + oRemotePath + "' (" + e.Message + ").");
            }

            return bRet;
        }


        internal bool UploadFile(string strFilePath)
        {
            if (!strFilePath.StartsWith("\\"))
            {
                strFilePath = "\\" + strFilePath;
            }

            bool bRet = true;
            string strLocalPath = m_strWorkspacePath + strFilePath;
            Plugin_FTP.FilePath oRemotePath = new Plugin_FTP.FilePath(m_strFTPFullPath + "/" + m_strFTPPath + strFilePath);

            m_oPlugin.LogInfo("Uploading file from '" + strLocalPath + "' to '" + oRemotePath + "'.");

            try
            {
                string strParentPath = oRemotePath.ParentPath;

                if (m_oSFTPConn.ChangeWorkingDirectory(strParentPath))
                {
                    m_oSFTPConn.UploadFile(strLocalPath, oRemotePath.Name);
                }
                else
                {
                    bRet = false;
                    m_oPlugin.LogError("Failed to access remote directory '" + strParentPath + "'.");
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to upload file '" + strLocalPath + "' to '" + oRemotePath + "'. " + e.Message);
            }

            return bRet;

        }

        internal bool DeleteFile(string strFilePath)
        {
            bool bRet = true;
            Plugin_FTP.FilePath oRemotePath = new Plugin_FTP.FilePath(m_strFTPFullPath + "/" + m_strFTPPath + strFilePath);

            m_oPlugin.LogInfo("Deleting file '" + oRemotePath + "'.");

            try
            {
                string strParentPath = oRemotePath.ParentPath;

                if (m_oSFTPConn.ChangeWorkingDirectory(strParentPath))
                {
                    m_oSFTPConn.DeleteFile(oRemotePath.Name);
                }
                else
                {
                    bRet = false;
                    m_oPlugin.LogError("Failed to access remote directory '" + strParentPath + "'.");
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to delete file '" + strFilePath + "' (" + e.Message + ").");
            }

            return bRet;
        }

        internal bool DeleteDir(string strFolderPath)
        {
            bool bRet = true;
            Plugin_FTP.FilePath oRemotePath = new Plugin_FTP.FilePath(m_strFTPFullPath + "/" + m_strFTPPath + strFolderPath);

            m_oPlugin.LogInfo("Deleting folder '" + oRemotePath + "'.");

            try
            {
                string strParentPath = oRemotePath.ParentPath;

                if (m_oSFTPConn.ChangeWorkingDirectory(strParentPath))
                {
                    m_oSFTPConn.DeleteDirectoryTree(oRemotePath.Name);
                }
                else
                {
                    bRet = false;
                    m_oPlugin.LogError("Failed to access remote directory '" + strParentPath + "'.");
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to delete folder '" + strFolderPath + "' (" + e.Message + ").");
            }

            return bRet;
        }

        internal bool CheckFTPFolderExists(string strRemoteDirName, bool bTryAndCreate = true)
        {
            bool bRet = true;
            bool bTryCreate = false;
            m_oPlugin.LogInfo("Checking whether ftp folder folder '" + strRemoteDirName + "' exists.");

            try
            {
                string[] strFiles = m_oSFTPConn.GetFiles(strRemoteDirName);

                foreach (string strFile in strFiles)
                {
                    if (!(strFile == "." ||
                          strFile == ".."))
                    {
                        break;
                    }
                }
            }
            catch(EnterpriseDT.Net.Ftp.FTPException e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to check if folder '" + strRemoteDirName + "' exists (" + e.Message + ").");

                if (e.ReplyCode == 550 && bTryAndCreate)
                {
                    bTryCreate = true;
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to check if folder '" + strRemoteDirName + "' exists (" + e.Message + ").");
            }

            try
            {
                if (bTryCreate)
                {
                    m_oSFTPConn.CreateDirectory(strRemoteDirName);

                    bRet = CheckFTPFolderExists(strRemoteDirName, false);
                }
            }
            catch (Exception e)
            {
                bRet = false;
                m_oPlugin.LogError("Failed to create ftp directory '" + strRemoteDirName + "' (" + e.Message + ").");
            }

            return bRet;
        }

        internal Workspace FindWorkspace(Repository oRepos, bool bCreate)
        {
            // See if the workspace exists
            {
                Workspaces oWorkspaces = oRepos.Workspaces;

                m_oPlugin.LogInfo("Looking for workspace '" + m_strWorkspacePath + "'.");

                foreach (Workspace oWorkspace in oWorkspaces)
                {
                    if ( oWorkspace.ManagesPath( m_strWorkspacePath ) )
                    {
                        m_oPlugin.LogInfo("Found workspace '" + m_strWorkspacePath + "'.");
                        return oWorkspace;
                    }
                }

                m_oPlugin.LogInfo("Failed to find workspace for path '" + m_strWorkspacePath + "'.");
            }

            if (bCreate)
            {
                // The workspace doesn't exist - so try and create it
                m_oPlugin.LogInfo("Workspace '" + m_strWorkspacePath + "' does not exist. Will try and create...");

                PureCM.Client.Stream oStream = oRepos.Streams.ByPath(m_strStreamName);

                if (oStream != null)
                {
                    if (oStream.CreateWorkspace("", m_strWorkspacePath, "Deployment Workspace", false, false, true ))
                    {
                        m_oPlugin.LogInfo("Workspace '" + m_strWorkspacePath + "' has been created.");

                        Workspace oWS = FindWorkspace(oRepos, false);

                        if (oWS != null)
                        {
                            if (Connect())
                            {
                                if (UploadRootDir())
                                {
                                    return oWS;
                                }
                                else
                                {
                                    m_oPlugin.LogError("Failed to upload files after creating workspace '" + m_strWorkspacePath + "'.");

                                    if (!oWS.Delete(true, false))
                                    {
                                        m_oPlugin.LogError("Failed to delete created workspace '" + m_strWorkspacePath + "'.");
                                    }
                                }

                                Disconnect();
                            }
                            else
                            {
                                m_oPlugin.LogError("Failed to connect to ftp server - so unable to upload all files for workspace '" + m_strWorkspacePath + "'.");
                            }
                        }
                        else
                        {
                            m_oPlugin.LogError("After creating workspace '" + m_strWorkspacePath + "' the workspace could not be found!");
                        }
                    }
                    else
                    {
                        m_oPlugin.LogError("Failed to create workspace '" + m_strWorkspacePath + "'.");
                    }
                }
                else
                {
                    m_oPlugin.LogError("Failed to create workspace '" + m_strWorkspacePath + "'. The stream '" + m_strStreamName + "' is invalid.");
                }
            }

            return null;
        }

    }
}

