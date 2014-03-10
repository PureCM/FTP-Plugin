using System;
using System.Text;

namespace Plugin_FTP
{
    class FilePath
    {
        private String m_strPath;

        public FilePath()
        {
            m_strPath = String.Empty;
        }

        public FilePath(string strPath)
        {
            m_strPath = strPath;
        }

        public String Path
        {
            get
            {
                return m_strPath;
            }

            set
            {
                m_strPath = value;
            }
        }

        public void Append(String strName)
        {
            m_strPath += strName;
        }

        public void RemoveLastItem()
        {
            m_strPath = ParentPath;
        }

        public String Name
        {
            get
            {
                char[] acToken = new char[2];

                acToken[0] = '\\';
                acToken[1] = '/';

                string[] astrPath = m_strPath.Split(acToken);

                if (astrPath.Length >= 1)
                {
                    return astrPath[astrPath.Length - 1];
                }

                return m_strPath;
            }
        }

        public String ParentFolderName
        {
            get
            {
                char[] acToken = new char[2];

                acToken[0] = '\\';
                acToken[1] = '/';

                string[] astrPath = m_strPath.Split(acToken);

                if (astrPath.Length >= 2)
                {
                    return astrPath[astrPath.Length - 2];
                }

                return String.Empty;
            }
        }

        public String ParentPath
        {
            get
            {
                char[] acToken = new char[2];

                acToken[0] = '\\';
                acToken[1] = '/';

                string[] astrPath = m_strPath.Split(acToken);

                String strRet = "";

                for (int i = 0; i < (astrPath.Length - 1); i++)
                {
                    strRet += astrPath[i];
                    strRet += "/";
                }

                return strRet;
            }
        }

        public bool IsParentOf(FilePath oPath)
        {
            return IsParentOf(oPath.Path);
        }

        public bool IsParentOf(string strPath)
        {
            return IsSubFolder(m_strPath, strPath);
        }

        public bool IsChildOf(FilePath oPath)
        {
            return IsChildOf(oPath.Path);
        }

        public bool IsChildOf(string strPath)
        {
            return IsSubFolder(strPath, m_strPath);
        }

        public static bool IsSubFolder(string strPath1, string strPath2)
        {
            char[] acToken = new char[2];

            acToken[0] = '\\';
            acToken[1] = '/';

            string[] astrPath1 = strPath1.Split(acToken);
            string[] astrPath2 = strPath2.Split(acToken);

            if (astrPath1.Length < astrPath2.Length)
            {
                for (int i = 0; i < astrPath1.Length; i++)
                {
                    if (astrPath2[i] != astrPath1[i])
                        return false;
                }

                return true;
            }

            return false;
        }

        public override string ToString()
        {
            return m_strPath;
        }
    }
}

