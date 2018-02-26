﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Ogam3.Lsp;
using Ogam3.Lsp.Generators;
using Ogam3.TxRx;
using Ogam3.Utils;

namespace Ogam3.Network.Tcp {
    public class OTcpClient : ISomeClient{
        public string Host;
        public int Port;

        public TcpClient ClientTcp;
        private Transfering Transfering;
        private static int timeout = 60000;
        public uint BufferSize = 1048576;
        public NetworkStream Stream;
        private Evaluator Evaluator;

        private readonly Synchronizer _connSync = new Synchronizer(true);
        private readonly Synchronizer _sendSync = new Synchronizer(true);

        public OTcpClient(string host, int port, Evaluator evaluator = null) {
            Host = host;
            Port = port;

            if (evaluator == null) {
                Evaluator = new Evaluator();
            }

            new Thread(() => {
                while (true) {
                    ConnectServer();
                    _connSync.Wait();
                }
            }) {IsBackground = true}.Start();
        }

        public T CreateInterfase<T>() {
            return (T)RemoteCallGenertor.CreateTcpCaller(typeof(T), this);
        }

        public void RegisterImplementation(object instanceOfImplementation) {
            //Definer.Define(Evaluator.DefaultEnviroment, instanceOfImplementation);
            ClassRegistrator.Register(Evaluator.DefaultEnviroment, instanceOfImplementation);
        }

        private TcpClient ConnectTcp() {
            while (true) {
                try {
                    ClientTcp?.Close();
                    ClientTcp = new TcpClient();
                    ClientTcp.Connect(Host, Port);

                    break; // connection success
                }
                catch (Exception e) {
                    ClientTcp?.Close();
                    Thread.Sleep(1000); // sleep reconnection
                }
            }

            return ClientTcp;
        }

        public Action ConnectionStabilised;
        public event Action<Exception> ConnectionError;

        private void ConnectServer() {
            var ns = new NetStream(ConnectTcp());

            Transfering?.Dispose();
            Transfering = new Transfering(ns, ns, BufferSize);

            Transfering.ConnectionStabilised = OnConnectionStabilised;

            Transfering.ConnectionError = ex => {
                lock (Transfering) {
                    // for single raction
                    Transfering.ConnectionError = null;

                    _sendSync.Lock();
                    Console.WriteLine($"Connection ERROR {ex.Message}");
                    OnConnectionError(ex);

                    _connSync.Pulse();
                }
            };

            Transfering.StartReceiver(data => OTcpServer.DataHandler(Evaluator, data));

            _sendSync.Unlock();
        }

        public event Action<SpecialMessage, object> SpecialMessageEvt;

        protected void OnSpecialMessageEvt(SpecialMessage sm, object call) {
            SpecialMessageEvt?.Invoke(sm, call);
        }

        public object Call(object seq) {
            if (_sendSync.Wait(5000)) {
                var resp = BinFormater.Read(new MemoryStream(Transfering.Send(BinFormater.Write(seq).ToArray())));

                if (resp.Car() is SpecialMessage) {
                    OnSpecialMessageEvt(resp.Car() as SpecialMessage, seq);
                    return null;
                }

                return resp.Car();
            }
            else {
                // TODO connection was broken
                Console.WriteLine("Call error");
                OnConnectionError(new Exception("Call error"));
                return null;
            }
        }

        protected virtual void OnConnectionStabilised() {
            ConnectionStabilised?.Invoke();
        }

        protected virtual void OnConnectionError(Exception ex) {
            ConnectionError?.Invoke(ex);
        }
    }
}
