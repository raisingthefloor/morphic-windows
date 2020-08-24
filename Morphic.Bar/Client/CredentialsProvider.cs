// CredentialsProvider.cs: Gets credentials for the Morphic Service
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Client
{
    using System;
    using System.Threading.Tasks;
    using UI;

    public class CredentialsProvider
    {
        /// <summary>
        /// Get some credentials.
        /// </summary>
        /// <param name="serverHost">The host being connected to.</param>
        /// <param name="lastCredentials">The last credentials used (when failed).</param>
        /// <returns></returns>
        public async Task<Credentials> GetCredentials(string serverHost, Credentials? lastCredentials)
        {
            Credentials? credentials;
            if (lastCredentials == null)
            {
                credentials = new UserPasswordCredentials()
                {
#warning Hard-coded password
                    ServerHost = serverHost,
                    UserName = "",
                    Password = ""
                };
            }
            else
            {
                credentials = lastCredentials as UserPasswordCredentials ?? new UserPasswordCredentials()
                {
                    ServerHost = serverHost
                };

                await credentials.Get();
            }

            return credentials;
        }
    }

    public abstract class Credentials
    {
        public string? ServerHost { get; set; }
        public string? LastFailure { get; set; }
        public AuthRequest AuthRequest { get; }

        public event EventHandler? Success;
        public event EventHandler? Failure;
        public event EventHandler? Cancelled;

        protected Credentials(AuthRequest authRequest)
        {
            this.AuthRequest = authRequest;
        }

        /// <summary>
        /// Returns information suitable for logging.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.AuthRequest.GetType().Name;
        }
        
        public void OnSuccess()
        {
            this.Success?.Invoke(this, EventArgs.Empty);
        }
        
        public void OnCancelled()
        {
            this.Cancelled?.Invoke(this, EventArgs.Empty);
        }
        
        public void OnFailure(string failureMessage)
        {
            this.LastFailure = failureMessage;
            this.Failure?.Invoke(this, EventArgs.Empty);
        }
        
        public virtual Task Get()
        {
            return Task.CompletedTask;
        }
    }

    public class UserPasswordCredentials : Credentials
    {
        private LoginWindow? loginWindow;

        public string UserName
        {
            get => this.AuthRequest.UserName;
            set => this.AuthRequest.UserName = value;
        }

        public string Password
        {
            get => this.AuthRequest.Password;
            set => this.AuthRequest.Password = value;
        }

        public override string ToString()
        {
            bool hasPassword = string.IsNullOrEmpty(this.Password);
            return $"[{base.ToString()}:username={this.UserName},password={hasPassword}]";
        }

        public UserPasswordCredentials() : base(new AuthRequest())
        {
        }

        public override async Task Get()
        {
            if (this.LastFailure != null)
            {
                this.loginWindow ??= new LoginWindow(this);
                await this.loginWindow.GetCredentials();
            }
        }
    }
}
