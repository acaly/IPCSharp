using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IPCSharp
{
    public class Channel
    {
        private static readonly Crc32 _crc16 = new Crc32();
        private static readonly string _commonPrefix = "ipcs_";
        private readonly string _id;

        internal Channel(string id)
        {
            _id = id;
        }

        internal string GetId() => _id;
        internal string GetId(string subId) => _id + "_" + subId;
        public Channel GetSubChannel(string subId) => new Channel(_id + "_" + subId);

        public static Channel FromString(string id) => new Channel(_commonPrefix + id);

        public static Channel FromHash(string name)
        {
            return FromString(_crc16.ComputeChecksumString(Encoding.Unicode.GetBytes(name)));
        }

        public static Channel FromExecutablePath()
        {
            var path = Assembly.GetEntryAssembly().Location;
            return FromHash(path);
        }
    }
}
