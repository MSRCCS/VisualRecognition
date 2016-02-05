using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;

namespace NlpToolkit
{
    // finite state machine implementation

    // state type : int
    // supported transition code type : int, char, string

    public class FlexibleFsmState<Key>
        where Key : System.IComparable<Key>
    {
        public Key Code;
        public int StateValue = int.MinValue;
        public Dictionary<Key, FlexibleFsmState<Key>> dicCode2State;
    }

    public class FiniteStateMachineGenerator<T>
        where T : IComparable<T>
    {
        public FlexibleFsmState<T> Root = new FlexibleFsmState<T>();
        public int StateCount = 1;

        public void SetItem(IEnumerable<T> listOfCodes, int stateValue, bool overwrite)
        {
            FlexibleFsmState<T> current = GetItem(listOfCodes, true);
            if (current.StateValue == int.MinValue || overwrite)
            {
                current.StateValue = stateValue;
            }
        }

        public FlexibleFsmState<T> GetItem(IEnumerable<T> listOfCodes, bool autoExpand)
        {
            FlexibleFsmState<T> current = Root;
            if (autoExpand)
            {
                foreach (var code in listOfCodes)
                {
                    if (current.dicCode2State == null)
                    {
                        current.dicCode2State = new Dictionary<T, FlexibleFsmState<T>>();
                    }
                    if (!current.dicCode2State.ContainsKey(code))
                    {
                        FlexibleFsmState<T> s = new FlexibleFsmState<T>() { Code = code };
                        current.dicCode2State.Add(code, s);
                        ++StateCount;
                    }
                    current = current.dicCode2State[code];
                }
            }
            else
            {
                foreach (var code in listOfCodes)
                {
                    if (current.dicCode2State == null || !current.dicCode2State.ContainsKey(code))
                    {
                        current = null;
                        break;
                    }
                    current = current.dicCode2State[code];
                }
            }
            return current;
        }

        public void Save(string filename)
        {
            FiniteStateMachine<T> fsm = new FiniteStateMachine<T>(StateCount);

            Queue<FlexibleFsmState<T>> queue = new Queue<FlexibleFsmState<T>>();
            queue.Enqueue(Root);
            int maxStateId = 0, currentStateId = 0;
            while (queue.Count > 0)
            {
                FlexibleFsmState<T> s = queue.Dequeue();
                int len = s.dicCode2State == null ? 0 : s.dicCode2State.Count;

                fsm.Codes[currentStateId] = s.Code;
                fsm.States[currentStateId] = new CompactFsmState()
                {
                    Start = maxStateId + 1,
                    Len = len,
                    StateValue = s.StateValue
                };
                ++currentStateId;

                if (len > 0)
                {
                    foreach (var p in s.dicCode2State.OrderBy(p => p.Key))
                    {
                        queue.Enqueue(p.Value);
                    }
                    maxStateId += len;
                }
            }

            fsm.Save(filename);

            Console.WriteLine("Save() : DONE, StateCount={0}", StateCount);
        }
    }

    interface IDataFormatter<T>
    {
        T Read(BinaryReader br);
        void Write(BinaryWriter bw, T value);
    }
    class Int32Formatter : IDataFormatter<int>
    {
        public int Read(BinaryReader br) { return br.ReadInt32(); }
        public void Write(BinaryWriter bw, int value) { bw.Write(value); }
    }
    class CharFormatter : IDataFormatter<char>
    {
        public char Read(BinaryReader br) { return br.ReadChar(); }
        public void Write(BinaryWriter bw, char value) { bw.Write(value); }
    }
    class StringFormatter : IDataFormatter<string>
    {
        public string Read(BinaryReader br) { return br.ReadString(); }
        public void Write(BinaryWriter bw, string value) { bw.Write(value); }
    }
    public static class DataFormatterFactory
    {
        internal static IDataFormatter<T> GetDataFormatter<T>()
        {
            Type codeType = typeof(T);
            if (codeType == typeof(int))
            {
                return (IDataFormatter<T>)new Int32Formatter();
            }
            else if (codeType == typeof(char))
            {
                return (IDataFormatter<T>)new CharFormatter();
            }
            else if (codeType == typeof(string))
            {
                return (IDataFormatter<T>)new StringFormatter();
            }
            else
            {
                throw new ApplicationException("Unsupported data type in FSM : " + codeType.ToString());
            }
        }
    }

    public class MatchedItem
    {
        public int Start;
        public int Len;
        public int StateValue;

        public IEnumerable<_Key> GetCodes<_Key>(_Key[] Keys)
        {
            return Keys.Skip(Start).Take(Len);
        }
    }

    struct CompactFsmState
    {
        public int StateValue;
        public int Start;
        public int Len;
    }

    public class FiniteStateMachine<T>
        where T : IComparable<T>
    {
        internal int StateCount { get { return Codes.Length; } }
        internal T[] Codes;
        internal CompactFsmState[] States;

        IDataFormatter<T> DataFormater;

        public FiniteStateMachine(int stateCount)
        {
            Codes = new T[stateCount];
            States = new CompactFsmState[stateCount];

            DataFormater = DataFormatterFactory.GetDataFormatter<T>();
        }

        public void Save(string filename)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Create(filename)))
            {
                bw.Write(StateCount);

                for (int i = 0; i < StateCount; ++i)
                {
                    if (i > 0) DataFormater.Write(bw, Codes[i]);
                    bw.Write(States[i].Start);
                    bw.Write(States[i].Len);
                    bw.Write(States[i].StateValue);
                }
            }
        }

        public static FiniteStateMachine<T> Load(string filename)
        {
            using (BinaryReader br = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                int stateCount = br.ReadInt32();

                FiniteStateMachine<T> fsm = new FiniteStateMachine<T>(stateCount);
                for (int i = 0; i < stateCount; ++i)
                {
                    if (i > 0) fsm.Codes[i] = fsm.DataFormater.Read(br);
                    fsm.States[i].Start = br.ReadInt32();
                    fsm.States[i].Len = br.ReadInt32();
                    fsm.States[i].StateValue = br.ReadInt32();
                }
                return fsm;
            }
        }

        public IEnumerable<MatchedItem> GetLongestMatches(T[] tokens, int from = 0, int to = int.MaxValue)
        {
            for (int i = from; i < tokens.Length && i < to; ++i)
            {
                int current = 0, longestOffset = -1, longestStateId = -1;
                for (int offset = 0; i + offset < tokens.Length; ++offset)
                {
                    int next = Array.BinarySearch(Codes, States[current].Start, States[current].Len, tokens[i + offset]);

                    if (next >= 0)
                    {
                        current = next;

                        if (States[current].StateValue != int.MinValue)
                        {
                            longestOffset = offset + 1;
                            longestStateId = current;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (longestOffset > 0)
                {
                    yield return new MatchedItem
                    {
                        Start = i,
                        Len = longestOffset,
                        StateValue = States[longestStateId].StateValue
                    };
                    i += longestOffset - 1; // -1 to avoid double add in the for loop, added by leizhang, 11/11/2014
                }
                else
                {
                    //++i;  // to avoid double add in the for loop, added by leizhang, 11/11/2014
                }
            }
        }

        public IEnumerable<MatchedItem> GetMatches(T[] tokens, int from = 0, int to = int.MaxValue)
        {
            for (int i = from; i < tokens.Length && i < to; ++i)
            {
                int current = 0;
                for (int offset = 0; i + offset < tokens.Length; ++offset)
                {
                    int next = Array.BinarySearch(Codes, States[current].Start, States[current].Len, tokens[i + offset]);

                    if (next >= 0)
                    {
                        current = next;

                        if (States[current].StateValue != int.MinValue)
                        {
                            yield return new MatchedItem
                            {
                                Start = i,
                                Len = offset + 1,
                                StateValue = States[current].StateValue
                            };
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }

    public class FiniteStateMachineGenerator<T, Value>
        where T : IComparable<T>
    {
        public FiniteStateMachineGenerator<T> FsmGenerator = new FiniteStateMachineGenerator<T>();
        public List<Value> Values = new List<Value>();

        public void SetItem(IEnumerable<T> listOfCodes, Value stateValue, bool overwrite)
        {
            FlexibleFsmState<T> node = FsmGenerator.GetItem(listOfCodes, true);

            if (node.StateValue == int.MinValue)
            {
                node.StateValue = Values.Count;
                Values.Add(stateValue);
            }
            else
            {
                Values[node.StateValue] = stateValue;
            }
        }

        public FlexibleFsmState<T> GetItem(IEnumerable<T> listOfCodes, bool autoExpand)
        {
            return FsmGenerator.GetItem(listOfCodes, autoExpand);
        }

        public void Save(string filename, int batchSize = 100*1024)
        {
            FsmGenerator.Save(filename);

            using (BinaryWriter bw = new BinaryWriter(File.Create(filename + ".values")))
            {
                bw.Write(Values.Count);
                bw.Write(batchSize);

                IFormatter formatter = new BinaryFormatter();
                for (int i = 0; i < (Values.Count + batchSize - 1) / batchSize; ++i)
                {
                    var buffer = Values.Skip(i * batchSize).Take(batchSize).ToArray();
                    formatter.Serialize(bw.BaseStream, buffer);
                }
            }
        }
    }

    public class MatchedItem<T> : MatchedItem
    {
        public T Value1;
    }
    public class FiniteStateMachine<T, ValueType>
        where T : IComparable<T>
    {
        FiniteStateMachine<T> Fsm;
        ValueType[] Values;

        FiniteStateMachine() { }
        public static FiniteStateMachine<T, ValueType> Load(string filename)
        {
            FiniteStateMachine<T, ValueType> ret = new FiniteStateMachine<T, ValueType>();
            ret.Fsm = FiniteStateMachine<T>.Load(filename);

            using (BinaryReader br = new BinaryReader(File.Open(filename + ".values", FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                int valueCount = br.ReadInt32();
                int batchSize = br.ReadInt32();

                ret.Values = new ValueType[valueCount];
                for (int i = 0; i < (valueCount + batchSize - 1) / batchSize; ++i)
                {
                    IFormatter formatter = new BinaryFormatter();
                    var buffer = (ValueType[])formatter.Deserialize(br.BaseStream);
                    Array.Copy(buffer, 0, ret.Values, i * batchSize, buffer.Length);
                }
            }
            return ret;
        }

        public IEnumerable<MatchedItem<ValueType>> GetLongestMatches(T[] tokens)
        {
            foreach (var mi in Fsm.GetLongestMatches(tokens))
            {
                yield return new MatchedItem<ValueType>()
                {
                    Start = mi.Start,
                    Len = mi.Len,
                    StateValue = mi.StateValue,
                    Value1 = (ValueType)(Values[mi.StateValue])
                };
            }
        }

        public IEnumerable<MatchedItem<ValueType>> GetMatches(T[] tokens, int from, int to)
        {
            foreach (var mi in Fsm.GetMatches(tokens, from, to))
            {
                yield return new MatchedItem<ValueType>()
                {
                    Start = mi.Start,
                    Len = mi.Len,
                    StateValue = mi.StateValue,
                    Value1 = (ValueType)(Values[mi.StateValue])
                };
            }
        }

        public IEnumerable<MatchedItem<ValueType>> GetMatches(T[] tokens)
        {
            foreach (var mi in Fsm.GetMatches(tokens))
            {
                yield return new MatchedItem<ValueType>()
                {
                    Start = mi.Start,
                    Len = mi.Len,
                    StateValue = mi.StateValue,
                    Value1 = (ValueType)(Values[mi.StateValue])
                };
            }
        }
    }
}
