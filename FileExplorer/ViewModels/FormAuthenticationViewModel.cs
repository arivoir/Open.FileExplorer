using Open.FileSystemAsync;
using Open.WebDav;
using System;
using System.Linq;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FormAuthenticationViewModel : BaseViewModel
    {
        #region ** fields

        private string _server = "";
        private string _domain = "";
        private string _userName = "";
        private string _password = "";
        private bool _ignoreCertificateErrors = false;

        #endregion

        #region ** initialization

        public FormAuthenticationViewModel()
        {
            AuthorizeCommand = new TaskCommand(Authorize, CanAuthorize);
            DenyCommand = new TaskCommand(Deny, CanDeny);
            UserNameIsEmail = true;
            FormIsEnabled = true;
            ProgressBarVisible = false;
        }

        #endregion

        #region ** object model

        public string ProviderName { get; set; }
        public string BackgroundBrush { get; set; }
        public string ProviderIconKey { get; set; }
        public bool ServerVisible { get; set; }
        public bool DomainVisible { get; set; }
        public bool IgnoreCertificateErrorsVisible { get; set; }

        public string AuthorizationMessage
        {
            get
            {
                return string.Format(AuthenticationFormResources.FormAuthorizationMessage, ProviderName);
            }
        }

        public TaskCommand AuthorizeCommand { get; set; }

        public TaskCommand DenyCommand { get; set; }

        public Func<string, string, string, string, bool, Task<AuthenticatonTicket>> AuthorizeCallback { get; set; }
        public Func<Task> DenyCallback { get; set; }

        public string Server
        {
            get
            {
                return _server;
            }
            set
            {
                _server = value;
                OnPropertyChanged();
                AuthorizeCommand.OnCanExecuteChanged();
            }
        }

        public string Domain
        {
            get
            {
                return _domain;
            }
            set
            {
                _domain = value;
                OnPropertyChanged();
                AuthorizeCommand.OnCanExecuteChanged();
            }
        }

        public string UserName
        {
            get
            {
                return _userName;
            }
            set
            {
                _userName = value;
                OnPropertyChanged();
                AuthorizeCommand.OnCanExecuteChanged();
            }
        }

        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
                OnPropertyChanged();
                AuthorizeCommand.OnCanExecuteChanged();
            }
        }

        public bool IgnoreCertificateErrors
        {
            get
            {
                return _ignoreCertificateErrors;
            }
            set
            {
                _ignoreCertificateErrors = value;
                OnPropertyChanged();
            }
        }

        public bool UserAndPasswordRequired { get; set; }

        public bool UserNameIsEmail { get; set; }

        public AuthenticatonTicket Ticket { get; private set; }
        //        public InputScope UserNameInputScope
        //        {
        //            get
        //            {
        //                var inputScope = new InputScope();
        //                if (UserNameIsEmail)
        //#if NETFX_CORE
        //                    inputScope.Names.Add(new InputScopeName() { NameValue = InputScopeNameValue.EmailSmtpAddress });
        //#else
        //                    inputScope.Names.Add(new InputScopeName() { NameValue = InputScopeNameValue.EmailUserName });
        //#endif
        //                return inputScope;
        //            }
        //        }

        public bool FormIsEnabled { get; set; }

        public bool ProgressBarVisible { get; private set; }

        #endregion

        #region ** labels

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string ServerLabel
        {
            get
            {
                return AuthenticationFormResources.ServerLabel;
            }
        }

        public string DomainLabel
        {
            get
            {
                return AuthenticationFormResources.DomainLabel;
            }
        }

        public string UserNameLabel
        {
            get
            {
                if (UserNameIsEmail)
                    return AuthenticationFormResources.EmailLabel;
                else
                    return AuthenticationFormResources.UserNameLabel;
            }
        }

        public string PasswordLabel
        {
            get
            {
                return AuthenticationFormResources.PasswordLabel;
            }
        }

        public string AuthorizeLabel
        {
            get
            {
#if WINDOWS_PHONE
                return AuthenticationFormResources.AuthorizeLabel.ToLower();
#else
                return AuthenticationFormResources.AuthorizeLabel;
#endif
            }
        }

        public string IgnoreCertificateErrorsLabel
        {
            get
            {
                return AuthenticationFormResources.IgnoreCertificateErrorsLabel;
            }
        }

        public string IgnoreCertificateErrorsMessage
        {
            get
            {
                return AuthenticationFormResources.IgnoreCertificateErrorsMessage;
            }
        }

        public string PrivacyLinkUri
        {
            get
            {
                return PrivacyResources.PrivacyLinkUri;
            }
        }

        public string PrivacyLinkText
        {
            get
            {
                return PrivacyResources.PrivacyLinkText;
            }
        }

        public string ServerError
        {
            get
            {
                return GetErrors("Server").Cast<ValidationError>().FirstOrDefault()?.Message;
            }
        }

        public string DomainError
        {
            get
            {
                return GetErrors("Domain").Cast<ValidationError>().FirstOrDefault()?.Message;
            }
        }

        public string UserNameError
        {
            get
            {
                return GetErrors("UserName").Cast<ValidationError>().FirstOrDefault()?.Message;
            }
        }

        public string PasswordError
        {
            get
            {
                return GetErrors("Password").Cast<ValidationError>().FirstOrDefault()?.Message;
            }
        }

        #endregion

        #region ** implementation


        private bool CanAuthorize(object arg)
        {
            return true;
        }

        private async Task Authorize(object arg)
        {
            if (AuthorizeCallback != null)
            {
                Validate();

                if (!HasErrors)
                {
                    try
                    {
                        FormIsEnabled = false;
                        ProgressBarVisible = true;
                        OnPropertyChanged("FormIsEnabled");
                        OnPropertyChanged("ProgressBarVisible");
                        Ticket = await AuthorizeCallback(Server, Domain, UserName, Password, IgnoreCertificateErrors);
                    }
                    catch (AccessDeniedException)
                    {
                        SetError(new ValidationError(AuthenticationFormResources.AuthenticationDeniedMessage), "");
                    }
                    catch (WebDavException exc)
                    {
                        if (exc.StatusCode == 404)
                        {
                            SetError(new ValidationError(AuthenticationFormResources.ServerNotFoundMessage), "Server");
                        }
                        else
                        {
                            SetError(new ValidationError(AuthenticationFormResources.AuthenticationDeniedMessage), "");
                        }
                    }
                    catch (Exception exc)
                    {
                        if (exc.Message.Contains("The certificate authority is invalid or incorrect"))
                        {
                            SetError(new ValidationError(AuthenticationFormResources.InvalidCertificateAuthorityMessage), "Server");
                        }
                        else
                        {
                            SetError(new ValidationError(AuthenticationFormResources.AuthenticationDeniedMessage), "");
                        }
                    }
                    finally
                    {
                        FormIsEnabled = true;
                        ProgressBarVisible = false;
                        OnPropertyChanged("FormIsEnabled");
                        OnPropertyChanged("ProgressBarVisible");
                    }
                }
            }
        }

        public void Validate()
        {
            ClearErrors();
            if (ServerVisible)
            {
                if (string.IsNullOrWhiteSpace(Server))
                {
                    SetError(new ValidationError(FileSystemResources.RequiredLabel), "Server");
                }
                else if (!Uri.IsWellFormedUriString(Server, UriKind.Absolute))
                {
                    SetError(new ValidationError(AuthenticationFormResources.InvalidWebAddressMessage), "Server");
                }
            }
            if (UserAndPasswordRequired)
            {
                if (string.IsNullOrWhiteSpace(UserName))
                {
                    SetError(new ValidationError(FileSystemResources.RequiredLabel), "UserName");
                }
                if (string.IsNullOrWhiteSpace(Password))
                {
                    SetError(new ValidationError(FileSystemResources.RequiredLabel), "Password");
                }
            }
        }

        private bool CanDeny(object arg)
        {
            return true;
        }

        private async Task Deny(object arg)
        {
            await DenyCallback();
        }

        #endregion
    }
}
