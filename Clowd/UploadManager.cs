﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using RT.Util.ExtensionMethods;
using Clowd.Shared;

namespace Clowd
{
    public static class UploadManager
    {
        public static int UploadsInProgress { get { return _window.Value.Uploads.Where(up => up.Progress < 100 && up.UploadFailed == false).Count(); } }
        public static bool UploadWindowVisible { get { return _window.Value.IsVisible; } }
        public static bool Authenticated { get { return _cache != null && (_cache.Credentials != null || !String.IsNullOrWhiteSpace(_cache.SessionKey)); } }

        private const string _contentHeader = "CONTENT-LENGTH";
        private static Lazy<UploadsWindow> _window = new Lazy<UploadsWindow>();
        private static SessionCredentialStore _cache;
        public static async Task<string> Upload(byte[] data, string displayName, UploadOptions options = null)
        {
            var uploadBar = CreateNewUpload(displayName);
            UploadSession context;
            try
            {
                using (context = await GetSession(true))
                {
                    Packet p = new Packet();
                    p.Command = "UPLOAD";
                    p.Headers.Add("content-type", "file");
                    p.Headers.Add("data-hash", MD5.Compute(data));
                    p.Headers.Add("display-name", displayName);

                    if (options?.DirectEnabled == true)
                        p.Headers.Add("direct", "true");
                    if (options?.ViewLimit > 0)
                        p.Headers.Add("view-limit", options.ViewLimit.ToString());
                    if (options?.ValidDuration != null)
                        p.Headers.Add("valid-for", options.ValidDuration.Value.Ticks.ToString());

                    const int chunk_size = 65535;
                    int data_size = data.Count();

                    if (data_size > chunk_size)
                    {
                        p.Headers.Add("partitioned", "true");
                        p.Headers.Add("file-size", data_size.ToString());

                        for (int i = 0; i < data_size; i += chunk_size)
                        {
                            var size = Math.Min(chunk_size, data_size - i);
                            bool last = i + chunk_size >= data_size;
                            byte[] buffer = new byte[size];
                            double progress = ((double)i + size) / data_size * 100;
                            Buffer.BlockCopy(data, i, buffer, 0, size);

                            if (i == 0)
                            {
                                p.PayloadBytes = buffer;
                                await context.WriteAsync(p);
                                var initial = await context.WaitPacketAsync();
                                if (initial.Command != "CONTINUE")
                                    throw new NotImplementedException();
                                if (initial.Headers.ContainsKey("display-name"))
                                    uploadBar.DisplayText = initial.Headers["display-name"];
                                uploadBar.ActionLink = initial.Payload;
                                uploadBar.ActionAvailable = true;
                            }
                            else
                            {
                                //await Task.Delay(500); //remove, this is for testing.
                                Packet chk = new Packet();
                                chk.Command = "CHUNK";
                                chk.Headers.Add("last", last ? "true" : "false");
                                chk.PayloadBytes = buffer;
                                await context.WriteAsync(chk);
                            }
                            UpdateUploadProgress(uploadBar, progress > 98 ? 98 : progress, i + chunk_size);
                        }
                    }
                    else
                    {
                        p.PayloadBytes = data;
                        UpdateUploadProgress(uploadBar, 50, data.Length / 2);
                        await context.WriteAsync(p);
                    }
                    var response = await context.WaitPacketAsync();
                    if (response.Command == "COMPLETE" && response.HasPayload)
                    {
                        if (response.Headers.ContainsKey("display-name"))
                            uploadBar.DisplayText = response.Headers["display-name"];
                        UpdateUploadProgress(uploadBar, 100, data.Length);
                        if (!UploadWindowVisible)
                            ShowUploadsWindow();
                        uploadBar.ActionLink = response.Payload;
                        uploadBar.ActionAvailable = true;
                        return response.Payload;
                    }
                    else
                        throw new NotImplementedException();
                }
            }
            catch (Exception e)
            {
                ErrorUploadProgress(uploadBar, e.Message);
                return null;
            }
        }

        public static async Task<UploadDTO[]> MyUploads(int offset = 0, int count = 10)
        {
            try
            {
                using (var context = await GetSession(true))
                {
                    Packet pa = new Packet("MYUPLOADS");
                    pa.Headers.Add("count", count.ToString());
                    pa.Headers.Add("offset", offset.ToString());
                    await context.WriteAsync(pa);
                    var resp = await context.WaitPacketAsync();
                    return RT.Util.Serialization.ClassifyJson.Deserialize<UploadDTO[]>(RT.Util.Json.JsonValue.Parse(resp.Payload));
                }
            }
            catch
            {
                return null;
            }
        }


        public static void RemoveUpload(string url, bool closeWindowIfOnly = true)
        {
            var search = _window.Value.Uploads.SingleOrDefault(up => up.ActionLink == url);
            if (search != null)
            {
                _window.Value.Uploads.Remove(search);
                if (closeWindowIfOnly && !_window.Value.Uploads.Any(up => up.Progress < 100))
                {
                    _window.Value.Hide();
                }
            }
        }
        public static void ShowUploadsWindow()
        {
            _window.Value.Show();
            if (!_window.Value.Uploads.Any(up => up.Progress < 100))
                _window.Value.CloseTimerEnabled = true;
        }


        public static async Task<AuthResult> Login(Credentials login)
        {
            using (var session = await GetSession(false))
            {
                if (session == null)
                    return AuthResult.NetworkError;

                var result = await session.CheckCredentials(login);
                if (result.Item1 == AuthResult.Success)
                {
                    _cache = new SessionCredentialStore()
                    {
                        Credentials = login.Clone(),
                        SessionKey = result.Item2
                    };
                }
                return result.Item1;
            }
        }
        public static void Logout()
        {
            _cache.Credentials.Dispose();
            _cache = null;
        }

        private static async Task<UploadSession> GetSession(bool login = true)
        {
            var session = await UploadSession.GetSession();
            if (session == null)
                return null;

            if (login && _cache != null)
            {
                AuthResult result = AuthResult.InvalidCredentials;
                if (!String.IsNullOrWhiteSpace(_cache.SessionKey))
                {
                    result = await session.CheckSessionKey(_cache.SessionKey);
                    if (result == AuthResult.Success)
                        return session;
                }
                if (_cache.Credentials != null)
                {
                    var seshresult = await session.CheckCredentials(_cache.Credentials);
                    result = seshresult.Item1;
                    if (result == AuthResult.Success)
                    {
                        _cache.SessionKey = seshresult.Item2;
                        return session;
                    }
                }
                if (result == AuthResult.NetworkError)
                {
                    session.Dispose();
                    return null;
                }
                //TODO: unable to auto-login with cached credentials...
                //show error? prompt for login if credentials are incorrect?
                _cache = null;
            }

            return session;
        }



        private static Controls.UploadProgressBar CreateNewUpload(string display)
        {
            var cnt = new Controls.UploadProgressBar();
            cnt.Foreground = System.Windows.Media.Brushes.PaleGoldenrod;
            cnt.DisplayText = "Connecting...";
            _window.Value.Uploads.Add(cnt);
            _window.Value.Show();
            _window.Value.CloseTimerEnabled = false;
            return cnt;
        }
        private static void UpdateUploadProgress(Controls.UploadProgressBar upload, double progress, long bytesWritten)
        {
            if (progress >= 100)
            {
                upload.ActionAvailable = true;
                upload.Foreground = System.Windows.Media.Brushes.PaleGreen;
            }
            upload.Progress = progress;
            upload.CurrentSizeDisplay = bytesWritten.ToPrettySizeString(0);
        }
        private static void ErrorUploadProgress(Controls.UploadProgressBar upload, string message)
        {
            upload.Foreground = System.Windows.Media.Brushes.PaleVioletRed;
            upload.UploadFailed = true;
            upload.ActionClicked = true;
            upload.Progress = 100;
        }

        private class SessionCredentialStore
        {
            public string SessionKey;
            public Credentials Credentials;
        }

        private class UploadSession : IDisposable
        {
            public CancellationTokenSource CancelToken { get; private set; }
            public NetworkStream WriteStream { get; private set; }
            private BufferBlock<Packet> PacketBuffer { get; set; }

            public bool Connected
            {
                get { return _client.Connected && !_readLoop.IsCompleted; }
            }
            public bool Authenticated { get; private set; } = false;
            public string Error { get; private set; }

            private TcpClient _client;
            private Task _readLoop;
            private static UploadSession _keep;
            private static readonly object _keepLock = new object();
            private static System.Windows.Threading.DispatcherTimer _idle;

            private UploadSession(TcpClient client)
            {
                _client = client;
                WriteStream = _client.GetStream();
                CancelToken = new CancellationTokenSource();
                PacketBuffer = new BufferBlock<Packet>();
                _readLoop = ReadLoopTcpClient(_client, CancelToken.Token, PacketBuffer);
                _readLoop.ContinueWith(rl => Dispose(true));
            }

            public static Task<UploadSession> GetSession()
            {
                return GetSession(false);
            }

            public static async Task<UploadSession> GetSession(bool forceNew)
            {
                if (!forceNew)
                {
                    UploadSession local = null;
                    lock (_keepLock)
                    {
                        if (_keep != null)
                        {
                            local = _keep;
                            _keep = null;
                            _idle.Stop();
                            _idle = null;
                        }
                    }
                    if (local != null)
                    {
                        if (local.Connected)
                            return local;
                        else
                            local.Dispose(true);
                    }
                }

                TcpClient tcp = new TcpClient();
                try
                {
                    await tcp.ConnectAsync(App.ServerHost, 12998);
                }
                catch
                {
                    tcp.Close();
                    return null;
                }
                return new UploadSession(tcp);
            }

            public async Task<Tuple<AuthResult, string>> CheckCredentials(Credentials cred)
            {
                try
                {
                    await this.WriteAsync(new Packet() { Command = "LOGIN", Payload = cred.Username });
                    var challenge = await this.WaitPacketAsync();
                    if (challenge == null || !challenge.Command.Equals("LOGINRESP"))
                        return new Tuple<AuthResult, string>(AuthResult.NetworkError, "");
                    if (challenge.Headers.ContainsKey("error"))
                        return new Tuple<AuthResult, string>(AuthResult.InvalidCredentials, "");

                    string iv = challenge.Headers["iv"];
                    string salt = challenge.Headers["salt"];
                    string authstring = MD5.Compute(MD5.Compute(cred.PasswordHash, salt), iv);

                    await this.WriteAsync(new Packet() { Command = "AUTH", Payload = authstring });
                    var authresp = await this.WaitPacketAsync();
                    if (authresp == null || !authresp.Command.Equals("AUTHRESP"))
                        return new Tuple<AuthResult, string>(AuthResult.NetworkError, "");
                    if (authresp.Headers.ContainsKey("error"))
                        return new Tuple<AuthResult, string>(AuthResult.InvalidCredentials, "");

                    Authenticated = true;
                    return new Tuple<AuthResult, string>(AuthResult.Success, authresp.Payload);
                }
                catch
                {
                    return new Tuple<AuthResult, string>(AuthResult.NetworkError, "");
                }
            }
            public async Task<AuthResult> CheckSessionKey(string session)
            {
                try
                {
                    if (!String.IsNullOrEmpty(session))
                    {
                        await this.WriteAsync(new Packet() { Command = "SESSIONCK", Payload = session });
                        var check = await this.WaitPacketAsync();
                        if (check == null || !check.Command.Equals("SESSIONCKRESP"))
                            return AuthResult.NetworkError;
                        if (check.Headers.ContainsKey("valid") && (check.Headers["valid"] == "1" || check.Headers["valid"] == "true"))
                        {
                            Authenticated = true;
                            return AuthResult.Success;
                        }
                    }
                    return AuthResult.InvalidCredentials;
                }
                catch { return AuthResult.NetworkError; }
            }


            public Task WriteAsync(Packet p)
            {
                var data = p.Serialize();
                return WriteStream.WriteAsync(data, 0, data.Length);
            }

            public Task<Packet> WaitPacketAsync()
            {
                return WaitPacketAsync(TimeSpan.FromSeconds(10));
            }
            public Task<Packet> WaitPacketAsync(TimeSpan time)
            {
                return PacketBuffer.ReceiveAsync(time, CancelToken.Token);
            }

            public void Dispose()
            {
                Dispose(false);
            }
            private void Dispose(bool forceDispose)
            {
                if (!forceDispose)
                {
                    lock (_keepLock)
                    {
                        if (_keep == null)
                        {
                            _keep = this;
                            _idle = new System.Windows.Threading.DispatcherTimer();
                            _idle.Interval = TimeSpan.FromSeconds(10);
                            _idle.Tick += (s, e) =>
                            {
                                _keep.Dispose(true);
                                _keep = null;
                                _idle.Stop();
                                _idle = null;
                            };
                            _idle.Start();
                            return;
                        }
                    }
                }
                CancelToken.Cancel();
                if (_client != null && _client.Connected)
                    _client.Close();
                WriteStream.Dispose();
                PacketBuffer.Complete();
            }

            private Task ReadLoopTcpClient(TcpClient client, CancellationToken token, BufferBlock<Packet> handlePacket)
            {
                return Task.Factory.StartNew(() =>
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        try
                        {
                            while (!token.IsCancellationRequested)
                            {
                                byte[] commandByte = stream.ReadUntil((byte)'\n', false);
                                if (commandByte == null) break;
                                string command = commandByte.FromUtf8();
                                if (String.IsNullOrWhiteSpace(command)) break;
                                var tmpDict = new SortedDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                                string tmpHeader;
                                while (!String.IsNullOrWhiteSpace(tmpHeader = stream.ReadUntil((byte)'\n', false).FromUtf8()))
                                {
                                    var split = tmpHeader.Split(':');
                                    tmpDict.Add(split[0].Trim(), split[1].Trim());
                                }
                                byte[] payload = new byte[0];
                                if (tmpDict.ContainsKey(_contentHeader))
                                {
                                    var contentlength = Convert.ToInt32(tmpDict[_contentHeader]);
                                    payload = new byte[contentlength];
                                    stream.FillBuffer(payload, 0, contentlength);
                                }
                                Packet p = new Packet(command, tmpDict, payload);
                                if (command.EqualsNoCase("ERROR")) // this is a fatal error
                                {
                                    Error = p.Payload;
                                    break;
                                }
                                else if (p.Headers.ContainsKey("error"))
                                    Error = p.Headers["error"];
                                handlePacket.Post(p);
                            }
                        }
                        catch { }
                    }
                }, token);
            }
        }
    }
    public enum AuthResult
    {
        Success,
        NetworkError,
        InvalidCredentials,
    }
    public class UploadOptions
    {
        public bool PublicVisible { get; set; } = true;
        public bool DirectEnabled { get; set; } = false;
        public TimeSpan? ValidDuration { get; set; } = null;
        public int ViewLimit { get; set; } = -1;
    }
    public class Credentials : ICloneable, IDisposable
    {
        public string Username
        {
            get
            {
                if (disposedValue)
                    throw new ObjectDisposedException("Credentials");
                return _username;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Username can not be empty.");
                _username = value;
            }
        }

        public string PasswordHash
        {
            get
            {
                if (disposedValue)
                    throw new ObjectDisposedException("Credentials");
                if (!String.IsNullOrWhiteSpace(_preHashed))
                    return _preHashed;
                if (_password.Length > 0)
                    return MD5.Compute(_password, _hardSalt);
                return "";
            }
        }

        private unsafe string _unsafePassword
        {
            get
            {
                IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(_password);
                try
                {
                    return new string((char*)ptr);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
                }
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Password can not be empty.");
                if (_password != null)
                    _password.Dispose();
                var secure = new System.Security.SecureString();
                for (int i = 0; i < value.Length; i++)
                {
                    secure.AppendChar(value[i]);
                }
                _password = secure;
            }
        }

        private const string _hardSalt = "29AcyQyeqJsQJLCt";
        private string _username;
        private string _preHashed;
        private System.Security.SecureString _password;

        public Credentials(string user, string pass, bool isInputHashed)
        {
            if (String.IsNullOrWhiteSpace(user))
                throw new ArgumentException("user cannot be empty");
            if (String.IsNullOrWhiteSpace(pass))
                throw new ArgumentException("pass cannot be empty");
            Username = user;
            if (isInputHashed)
            {
                _preHashed = pass;
            }
            else
            {
                _unsafePassword = pass;
            }
        }
        public Credentials(string user, System.Security.SecureString pass)
        {
            if (String.IsNullOrWhiteSpace(user))
                throw new ArgumentException("user cannot be empty");
            if (pass.Length < 1)
                throw new ArgumentException("pass cannot be empty");
            Username = user;
            _password = pass;
        }

        public Credentials Clone()
        {
            return new Credentials(Username, _password.Copy());
        }
        object ICloneable.Clone()
        {
            return new Credentials(Username, _password.Copy());
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _password.Dispose();
                    _password = null;
                    _username = null;
                    _preHashed = null;
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
