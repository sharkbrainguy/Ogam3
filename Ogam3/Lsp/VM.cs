﻿/*
 * Copyright © 2018 Tinkoff Bank
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

namespace Ogam3.Lsp {
    public class VirtualMashine {

        public static object Eval(Operation x, EnviromentFrame e) {
            return VM3(x, e);
        }
        
        public static object Eval(Operation x) {
            return VM3(x, new Core());
        }

        static object VM3(Operation operation, EnviromentFrame e) {
            object a = null;
            var x = operation;
            var rib = 0;

            var trueStack = new TrueStack(100);

            while (true) {
                switch (x.Cmd) {
                    case Operation.Comand.Halt:
                        return a;
                    case Operation.Comand.Refer: {
                        a = e.Get(x.Var);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Constant: {
                        a = x.Value;
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Close: {
                        a = new Closure(x.Branch2, e, x.Vars);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Test: {
                        x = GetSBool(a) == false ? x.Branch2 : x.Branch1;
                        break;
                    }
                    case Operation.Comand.Assign: {
                        e.Set(x.Var, a);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Extend: {
                        e.Define(x.Var, a);
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Conti: {
                        var var = "v".O3Symbol();
                        a = new Closure(Operation.Nuate(TrueStack.Clone(trueStack), var), new EnviromentFrame(), new [] {var});
                        x = x.Branch1;
                        break;
                    }
                    case Operation.Comand.Nuate: {
                        trueStack = TrueStack.Clone(x.Value as TrueStack);
                        a = e.Get(x.Var);
                        x = Operation.Return();
                        break;
                    }
                    case Operation.Comand.Frame: {
                        trueStack.Push(rib);
                        trueStack.Push(e);
                        trueStack.Push(x.Branch2);

                        x = x.Branch1;
                        
                        rib = 0;
                        break;
                    }
                    case Operation.Comand.Argument: {
                        trueStack.Push(a);
                        x = x.Branch1;
                        rib++;
                        break;
                    }
                    case Operation.Comand.Apply: {
                        if (a is MulticastDelegate) {
                            var func = a as MulticastDelegate;
                            var parameters = func.Method.GetParameters();

                            var argCnt = 0;
                            var cArg = new List<object>();
                            foreach (var pi in parameters) {
                                if (typeof(Params) == pi.ParameterType) {
                                    var par = new Params();
                                    while (argCnt++ < rib) {
                                        par.Add(trueStack.Pop());
                                    }
                                    cArg.Add(par);
                                }
                                else {
                                    if (argCnt++ < rib) {
                                        cArg.Add(trueStack.Pop());
                                    }
                                }
                            }

                            if (parameters.Length != cArg.Count) {
                                throw new Exception($"Arity mismatch {func}, expected {parameters.Length}, given {cArg.Count} arguments");
                            }

                            a = func.DynamicInvoke(cArg.ToArray());
                            x = Operation.Return();
                            break;
                        }

                        if (a is Closure) {
                            var func = a as Closure;
                            x = func.Body;
                            e = new EnviromentFrame(func.En);

                            if (rib != func.Argument.Length) {
                                throw new Exception($"Arity mismatch, expected {func.Argument.Length}, given {rib} arguments");
                            }

                            foreach (var arg in func.Argument) {
                                e.Define(arg, trueStack.Pop());
                            }

                            rib = 0;

                            break;
                        }

                        throw new Exception($"{a} is not a callable");
                    }
                    case Operation.Comand.Return: {
                        x = trueStack.Pop() as Operation;
                        e = trueStack.Pop() as EnviromentFrame;
                        rib = (int)trueStack.Pop();
                        break;
                    }
                }
            }
        }

        class TrueStack : Stack<object> {
            public TrueStack(int cap) : base(cap){}
            public TrueStack() { }
            private TrueStack(IEnumerable<object> imts) : base(imts){}
            public static TrueStack Clone(TrueStack original) {
                var arr = new object[original.Count];
                original.CopyTo(arr, 0);
                Array.Reverse(arr);
                return new TrueStack(arr);
            }
        }

        public static bool GetSBool(object o) {
            if (o == null)
                return false;
            if (o is bool)
                return (bool) o;
            else
                return true;
        }

        class Closure {
            public Symbol[] Argument;
            public Operation Body;
            public EnviromentFrame En;
            public int Arity;

            public Closure(Operation body, EnviromentFrame en, Symbol[] arguments) {
                Argument = arguments;
                En = en;
                Arity = arguments.Length;
                Body = body;
            }

            public EnviromentFrame Extend(List<object> r) {
                var argCnt = Arity;
                var callEnv = new EnviromentFrame(En);

                foreach (var arg in Argument) {
                    callEnv.Define(arg, r[--argCnt]);
                }

                return callEnv;
            }
        }
    }
}
