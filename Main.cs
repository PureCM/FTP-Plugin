using System;
using System.Collections.Generic;
using System.Text;
using PureCM.Server;
using PureCM.Client;
using System.Xml.Linq;
using System.IO;

namespace Plugin_FTP
{
    [EventHandlerDescription("Plugin that allows for deploying files using FTP")]
    public class FTPPlugin : PureCM.Server.Plugin
    {
        private FTPLink m_oFTPConnection = null;
        private Repository m_oPcmRepository = null;

        public override bool OnStart(XElement oConfig, Connection oConnection)
        {
            try
            {
                string strRepository;
                {
                    if (oConfig.Element("Repository") != null && oConfig.Element("Repository").Value.Length > 0)
                    {
                        strRepository = oConfig.Element("Repository").Value;

                        m_oPcmRepository = oConnection.Repositories.ByName(strRepository);

                        if (m_oPcmRepository == null)
                        {
                            LogError("The repository '" + strRepository + "' does not exist.");
                            return false;
                        }
                    }
                    else
                    {
                        LogError("You must specify a repository in the config file.");
                        return false;
                    }
                }

                string strStream;
                PureCM.Client.Stream oStream = null;
                {
                    if (oConfig.Element("Stream") != null && oConfig.Element("Stream").Value.Length > 0)
                    {
                        strStream = oConfig.Element("Stream").Value;

                        oStream = m_oPcmRepository.Streams.ByPath(strStream);

                        if (oStream == null)
                        {
                            LogError("The stream '" + strStream + "' does not exist.");
                            return false;
                        }
                    }
                    else
                    {
                        LogError("You must specify a stream in the config file.");
                        return false;
                    }
                }

                string strWorkspacePath;
                {
                    if (oConfig.Element("WorkspacePath") != null && oConfig.Element("WorkspacePath").Value.Length > 0)
                    {
                        strWorkspacePath = oConfig.Element("WorkspacePath").Value;
                    }
                    else
                    {
                        strWorkspacePath = Path.Combine(DataDirectory, "workspace");
                        LogInfo("You have not specified a workspace path. The workspace will be created in '" + strWorkspacePath + "'.");
                    }
                }

                string strFTPPath = "";
                {
                    if (oConfig.Element("Path") != null && oConfig.Element("Path").Value.Length > 0)
                    {
                        strFTPPath = oConfig.Element("Path").Value;
                    }
                    else
                    {
                        LogInfo("You have not specified the FTP path in the config file so the root will be used.");
                    }
                }

                string strFTPName;
                {
                    if (oConfig.Element("Server") != null && oConfig.Element("Server").Value.Length > 0)
                    {
                        strFTPName = oConfig.Element("Server").Value;
                    }
                    else
                    {
                        LogError("You must specify the FTP server name in the config file.");
                        return false;
                    }
                }

                string strFTPType = "ftp";
                {
                    if (oConfig.Element("Type") != null && oConfig.Element("Type").Value.Length > 0)
                    {
                        strFTPType = oConfig.Element("Type").Value;
                    }
                    else
                    {
                        LogInfo("You have not specified the FTP type in the config file so 'ftp' will be used.");
                    }
                }

                int nFTPPort = 21;
                {
                    if (oConfig.Element("Port") != null && oConfig.Element("Port").Value.Length > 0)
                    {
                        nFTPPort = int.Parse(oConfig.Element("Port").Value);

                        if (nFTPPort == 0)
                        {
                            LogError("The FTP port value '" + oConfig.Element("Port").Value + "' specified in the config file is invalid.");
                            return false;
                        }
                    }
                    else
                    {
                        LogInfo("You have not specified the FTP port in the config file so '21' will be used.");
                    }
                }

                string strFTPUser;
                {
                    if (oConfig.Element("Username") != null && oConfig.Element("Username").Value.Length > 0)
                    {
                        strFTPUser = oConfig.Element("Username").Value;
                    }
                    else
                    {
                        LogError("You must specify the FTP username in the config file.");
                        return false;
                    }
                }

                string strFTPPassword;
                {
                    if (oConfig.Element("Password") != null && oConfig.Element("Password").Value.Length > 0)
                    {
                        strFTPPassword = oConfig.Element("Password").Value;
                    }
                    else
                    {
                        LogError("You must specify the FTP password in the config file.");
                        return false;
                    }
                }

                bool bPassive = false;
                {
                    if (oConfig.Element("Passive") != null && oConfig.Element("Passive").Value.Length > 0)
                    {
                        bPassive = bool.Parse(oConfig.Element("Passive").Value);
                    }
                    else
                    {
                        LogInfo("You have not specified whether to use passive or active FTP in the config file so active will be used.");
                    }
                }

                bool bForceSync = false;
                {
                    if (oConfig.Element("ForceSync") != null && oConfig.Element("ForceSync").Value.Length > 0)
                    {
                        bForceSync = bool.Parse(oConfig.Element("ForceSync").Value);

                        if (bForceSync)
                        {
                            LogWarning("The config file has set force sync to true. This will upload all workspace files to ftp site on startup. This should only be performed as a one off operation if you experiencing problems.");
                        }
                    }
                }

                m_oFTPConnection = new FTPLink(this, strRepository, strStream, strWorkspacePath, strFTPPath, strFTPName, strFTPType, nFTPPort, strFTPUser, strFTPPassword, bPassive, bForceSync);

                if (!m_oFTPConnection.Validate(m_oPcmRepository))
                {
                    m_oFTPConnection = null;
                    return false;
                }

                oConnection.OnChangeSubmitted = OnChangeSubmitted;
                oConnection.OnStreamCreated = OnStreamCreated;
                oConnection.OnIdle = OnIdle;
            }
            catch( Exception e )
            {
                SDK.PCMSERVER_LogWarning(String.Format("Exception initialising FTP Plugin '{0}' Stack: '{1}'", e.Message, e.StackTrace));
                return false;
            }

            return true;
        }

        public override void OnStop()
        {
        }

        private void OnStreamCreated(StreamCreatedEvent evt)
        {
            try
            {
                if (evt.Repository != null)
                {
                    evt.Repository.RefreshStreams();
                }
            }
            catch (Exception e)
            {
                SDK.PCMSERVER_LogWarning(String.Format("Exception in FTP Plugin OnStreamCreated '{0}' Stack: '{1}'", e.Message, e.StackTrace));
            }
        }

        private void OnChangeSubmitted(ChangeSubmittedEvent evt)
        {
            try
            {
                Repository oRepos = evt.Repository;
                PureCM.Client.Stream oStream = evt.Stream;

                LogInfo("Change detected in Repository '" + oRepos.Name + "' " + "and Stream '" + oStream.Name + "'.");

                if (m_oFTPConnection != null && m_oFTPConnection.Compare(oRepos, oStream))
                {
                    if (m_oFTPConnection.Connect())
                    {
                        Workspace oWorkspace = m_oFTPConnection.FindWorkspace(oRepos, true);

                        if (oWorkspace != null)
                        {
                            m_oFTPConnection.UploadWorkspaceChanges(oWorkspace);
                        }
                    }

                    m_oFTPConnection.Disconnect();
                }
            }
            catch (Exception e)
            {
                SDK.PCMSERVER_LogWarning(String.Format("Exception in FTP Plugin OnChangeSubmitted '{0}' Stack: '{1}'", e.Message, e.StackTrace));
            }
        }

        private void OnIdle()
        {
            try
            {
                // On startup we want to check for any updates in OnIdle(). After we have done this once
                // we don't process OnIdle() anymore and we just wait for OnChangeSubmitted() events.
                // It would have been tidier to just do this in OnStart() but we want OnStart to return
                // quickly so it is shown as started. Remember that this might take some time because this
                // will create the initial workspace if necessary.
                if (m_oPcmRepository != null)
                {
                    if (m_oFTPConnection.Connect())
                    {
                        Workspace oWorkspace = m_oFTPConnection.FindWorkspace(m_oPcmRepository, true);

                        if (oWorkspace != null)
                        {
                            m_oFTPConnection.UploadWorkspaceChanges(oWorkspace);
                        }

                        // Setting m_oPcmRepository to null will ensure we don't process OnIdle() again
                        m_oPcmRepository = null;
                    }

                    m_oFTPConnection.Disconnect();
                }
            }
            catch (Exception e)
            {
                SDK.PCMSERVER_LogWarning(String.Format("Exception in FTP Plugin OnIdle '{0}' Stack: '{1}'", e.Message, e.StackTrace));
            }
        }
    }
}
