﻿/*
 * Copyright © 2015 - 2020 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EliteDangerousCore.DLL
{
    public class EDDDLLManager
    {
        public int Count { get { return DLLs.Count; } }
        public List<EDDDLLCaller> DLLs { get; private set; } = new List<EDDDLLCaller>();

        // search directory for *.dll, 
        // return loaded, failed, notallowed
        public Tuple<string, string, string> Load(string directory, string ourversion, string dllfolder, EDDDLLIF.EDDCallBacks callbacks, string allowed)
        {
            string loaded = "";
            string failed = "";
            string notallowed = "";

            if (!Directory.Exists(directory))
                failed = "DLL Folder does not exist";
            else
            {
                FileInfo[] allFiles = Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).Select(f => new FileInfo(f)).OrderBy(p => p.LastWriteTime).ToArray();

                string[] allowedfiles = allowed.Split(',');

                foreach (FileInfo f in allFiles)
                {
                    string filename = System.IO.Path.GetFileNameWithoutExtension(f.FullName);

                    EDDDLLCaller caller = new EDDDLLCaller();


                    if (caller.Load(f.FullName))        // if loaded (meaning it loaded, and its got EDDInitialise)
                    {
                        if (allowed.Equals("All", StringComparison.InvariantCultureIgnoreCase) || allowedfiles.Contains(filename, StringComparer.InvariantCultureIgnoreCase))    // if allowed..
                        {
                            if (caller.Init(ourversion, dllfolder, callbacks))       // must init
                            {
                                DLLs.Add(caller);
                                loaded = loaded.AppendPrePad(caller.Name, ",");
                            }
                            else
                            {
                                string errstr = caller.Version.HasChars() ? (": " + caller.Version.Substring(1)) : "";
                                failed = failed.AppendPrePad(caller.Name + errstr, ",");
                            }
                        }
                        else
                        {
                            notallowed = notallowed.AppendPrePad(caller.Name, ",");
                        }
                    }
                }
            }

            return new Tuple<string, string, string>(loaded, failed, notallowed);
        }

        public void UnLoad()
        {
            foreach (EDDDLLCaller caller in DLLs)
            {
                caller.UnLoad();
            }

            DLLs.Clear();
        }

        public void Refresh(string cmdr, EDDDLLIF.JournalEntry je)
        {
            foreach (EDDDLLCaller caller in DLLs)
            {
                caller.Refresh(cmdr, je);
            }
        }

        public void NewJournalEntry(EDDDLLIF.JournalEntry nje)
        {
            foreach (EDDDLLCaller caller in DLLs)
            {
                caller.NewJournalEntry(nje);
            }
        }

        public EDDDLLCaller FindCaller(string name)
        {
            return DLLs.Find(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        // item1 = true if found, item2 = true if caller implements.
        public Tuple<bool, bool> ActionJournalEntry(string dllname, EDDDLLIF.JournalEntry nje)
        {
            if (dllname.Equals("All", StringComparison.InvariantCultureIgnoreCase))
            {
                foreach (EDDDLLCaller caller in DLLs)
                    caller.ActionJournalEntry(nje);

                return new Tuple<bool, bool>(true, true);
            }
            else
            {
                EDDDLLCaller caller = FindCaller(dllname);
                return caller != null ? new Tuple<bool, bool>(true, caller.ActionJournalEntry(nje)) : new Tuple<bool, bool>(false, false);
            }
        }

        // List of DLL results, empty if no DLLs were found
        // else list of results. bool = true no error, false error.  String contains error string, or result string
        public List<Tuple<bool, string, string>> ActionCommand(string dllname, string cmd, string[] paras)
        {
            List<Tuple<bool, string, string>> resultlist = new List<Tuple<bool, string, string>>();

            if (dllname.Equals("All", StringComparison.InvariantCultureIgnoreCase))
            {
                foreach (EDDDLLCaller caller in DLLs)
                    resultlist.Add(AC(caller, cmd, paras));
            }
            else
            {
                EDDDLLCaller caller = FindCaller(dllname);
                if (caller != null)
                    resultlist.Add(AC(caller, cmd, paras));
                else
                    resultlist.Add(new Tuple<bool, string, string>(false, dllname, "Cannot find DLL "));
            }

            return resultlist;
        }

        private Tuple<bool, string, string> AC(EDDDLLCaller caller, string cmd, string[] paras)
        {
            string r = caller.ActionCommand(cmd, paras);
            if (r == null)
                return new Tuple<bool, string, string>(false, caller.Name, "DLL does not implement ActionCommand");
            else if (r.Length > 0 && r[0] == '+')
                return new Tuple<bool, string, string>(true, caller.Name, r.Mid(1));
            else
                return new Tuple<bool, string, string>(false, caller.Name, r.Mid(1));
        }
    }
}

