using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Plugin_FTP
{
    class FTPFileAction
    {
        public enum TFTPAction
        {
            tUploadFile,
            tDeleteFile,
            tUploadFolder,
            tDeleteFolder
        };

        public string m_strFilePath;
        public TFTPAction m_tAction;

        public String FilePath
        {
            get
            {
                return m_strFilePath.Replace('\\', '/');
            }
        }

        public FTPFileAction(string strFilePath, TFTPAction tAction)
        {
            m_strFilePath = strFilePath;
            m_tAction = tAction;
        }

        public bool Contains(string strFilePath, TFTPAction tNewAction)
        {
            if (m_strFilePath == strFilePath)
            {
                    return true;
            }
            else
            {
                if (m_tAction == TFTPAction.tUploadFile)
                {
                    if (tNewAction == TFTPAction.tUploadFolder ||
                        tNewAction == TFTPAction.tDeleteFolder)
                    {
                        return Plugin_FTP.FilePath.IsSubFolder(strFilePath, m_strFilePath);
                    }
                }
                else if (m_tAction == TFTPAction.tDeleteFile)
                {
                    if (tNewAction == TFTPAction.tDeleteFolder)
                    {
                        return Plugin_FTP.FilePath.IsSubFolder(strFilePath, m_strFilePath);
                    }
                }
                else if (m_tAction == TFTPAction.tUploadFolder)
                {
                    if (tNewAction == TFTPAction.tUploadFile ||
                        tNewAction == TFTPAction.tDeleteFolder)
                    {
                        return Plugin_FTP.FilePath.IsSubFolder(m_strFilePath, strFilePath);
                    }
                }
                else if (m_tAction == TFTPAction.tDeleteFolder)
                {
                    if (tNewAction == TFTPAction.tDeleteFile ||
                        tNewAction == TFTPAction.tUploadFolder)
                    {
                        return Plugin_FTP.FilePath.IsSubFolder(m_strFilePath, strFilePath);
                    }
                }
            }

            return false;
        }

        public void Update(string strFilePath, TFTPAction tNewAction)
        {
            if (m_tAction != tNewAction)
            {
                if (m_tAction == TFTPAction.tUploadFile)
                {
                    m_strFilePath = strFilePath;
                    m_tAction = tNewAction;
                }
                else if (m_tAction == TFTPAction.tDeleteFile)
                {
                    m_strFilePath = strFilePath;
                    m_tAction = tNewAction;
                }
                else if (m_tAction == TFTPAction.tUploadFolder)
                {
                    if (tNewAction == TFTPAction.tDeleteFolder)
                    {
                        m_strFilePath = strFilePath;
                        m_tAction = tNewAction;
                    }
                }
                else if (m_tAction == TFTPAction.tDeleteFolder)
                {
                    if (tNewAction == TFTPAction.tUploadFolder)
                    {
                        m_strFilePath = strFilePath;
                        m_tAction = tNewAction;
                    }
                }
            }

        }
    }
}
