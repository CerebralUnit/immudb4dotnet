using System.Collections.Generic;
using System.Text;
using CodeNotary.ImmuDb.ImmudbProto;

namespace CodeNotary.ImmuDb.Roots
{
    internal class RootHolder
    {
        private Dictionary<string, Root> rootMap = new Dictionary<string, Root>();

        internal Root GetRoot(string databaseName)
        {
            this.rootMap.TryGetValue(databaseName, out Root result);

            return result;
        }

        internal void SetRoot(string databaseName, Root root)
        {
            if (!this.rootMap.ContainsKey(databaseName))
            {
                this.rootMap.Add(databaseName, root);
            }
            else
            {
                this.rootMap[databaseName] = root;
            }
        }

        internal void FromByteArray(byte[] byteArray)
        {
            var stringModel = Encoding.UTF8.GetString(byteArray);

            this.rootMap = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Root>>(stringModel);
        }

        internal byte[] ToByteArray()
        {
            var stringModel = Newtonsoft.Json.JsonConvert.SerializeObject(this.rootMap);

            return Encoding.UTF8.GetBytes(stringModel);
        }
    }
}