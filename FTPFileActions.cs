using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Plugin_FTP
{
    class FTPFileActions
    {
        public ArrayList m_aoActions;

        public FTPFileActions()
        {
            m_aoActions = new ArrayList();
        }

        public void UploadFile(string strFilePath)
        {
            HandleAction(strFilePath, FTPFileAction.TFTPAction.tUploadFile);
        }

        public void DeleteFile(string strFilePath)
        {
            HandleAction(strFilePath, FTPFileAction.TFTPAction.tDeleteFile);
        }

        public void UploadFolder(string strFilePath)
        {
            HandleAction(strFilePath, FTPFileAction.TFTPAction.tUploadFolder);
        }

        public void DeleteFolder(string strFilePath)
        {
            HandleAction(strFilePath, FTPFileAction.TFTPAction.tDeleteFolder);
        }

        private void HandleAction(string strFilePath, FTPFileAction.TFTPAction tAction)
        {
            bool bFound = false;
            ArrayList aoRemoveActions = new ArrayList();

            foreach (FTPFileAction oAction in m_aoActions)
            {
                if (oAction.Contains(strFilePath, tAction))
                {
                    if (bFound)
                    {
                        aoRemoveActions.Add(oAction);
                    }
                    else
                    {
                        oAction.Update(strFilePath, tAction);
                    }

                    bFound = true;
                }
            }

            if (!bFound)
            {
                m_aoActions.Add(new FTPFileAction(strFilePath, tAction));
            }
            else
            {
                foreach (FTPFileAction oAction in aoRemoveActions)
                {
                    m_aoActions.Remove(oAction);
                }
            }
        }

        public string Print()
        {
            int nUpdatedFiles = 0;
            int nUpdatedFolders = 0;
            int nDeletedFiles = 0;
            int nDeletedFolders = 0;

            foreach (FTPFileAction oAction in m_aoActions)
            {
                switch (oAction.m_tAction)
                {
                    case FTPFileAction.TFTPAction.tUploadFile:
                        nUpdatedFiles++;
                        break;
                    case FTPFileAction.TFTPAction.tDeleteFile:
                        nDeletedFiles++;
                        break;
                    case FTPFileAction.TFTPAction.tUploadFolder:
                        nUpdatedFolders++;
                        break;
                    case FTPFileAction.TFTPAction.tDeleteFolder:
                        nDeletedFolders++;
                        break;
                }
            }

            return "Updated '" + nUpdatedFiles + "' Files and '" + nUpdatedFolders + "' Folders. Deleted '" + nDeletedFiles + "' Files and '" + nDeletedFolders + "' Folders.";
        }

    }
}
