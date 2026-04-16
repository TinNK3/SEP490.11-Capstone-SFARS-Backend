using System;
using System.Collections.Generic;
using System.Text;

namespace SFARS.Infrastructure.Helpers.Agora
{
    public class AccessToken2
    {
        private const string Version = "007";
        private const int VersionLength = 3;

        public string AppCert { get; set; } = "";
        public string AppId { get; set; } = "";
        public uint Expire { get; set; }
        public uint IssueTs { get; set; }
        public uint Salt { get; set; }
        public Dictionary<ushort, Service> Services { get; set; } = new Dictionary<ushort, Service>();

        public const short ServiceTypeRtc = 1;
        public const short ServiceTypeRtm = 2;
        public const short ServiceTypeFpa = 4;
        public const short ServiceTypeChat = 5;
        public const short ServiceTypeApaas = 7;

        public AccessToken2() { }

        public AccessToken2(string appId, string appCert, uint expire)
        {
            AppCert = appCert;
            AppId = appId;
            Expire = expire;
            IssueTs = (uint)Utils.getTimestamp();
            Salt = (uint)Utils.randomInt();
        }

        public void AddService(Service service)
        {
            Services[(ushort)service.GetServiceType()] = service;
        }

        public Service GetService(short serviceType)
        {
            return serviceType switch
            {
                ServiceTypeRtc => new ServiceRtc(),
                ServiceTypeRtm => new ServiceRtm(),
                ServiceTypeFpa => new ServiceFpa(),
                ServiceTypeChat => new ServiceChat(),
                ServiceTypeApaas => new ServiceApaas(),
                _ => throw new ArgumentException($"Unknown service type: {serviceType}")
            };
        }

        public static string GetUidStr(uint uid)
        {
            return uid == 0 ? "" : (uid & 0xFFFFFFFFL).ToString();
        }

        public string GetVersion() => Version;

        public byte[] GetSign()
        {
            byte[] signing = DynamicKeyUtil.encodeHMAC(BitConverter.GetBytes(IssueTs), Encoding.UTF8.GetBytes(AppCert), "SHA256");
            return DynamicKeyUtil.encodeHMAC(BitConverter.GetBytes(Salt), signing, "SHA256");
        }

        public string Build()
        {
            if (!Utils.isUUID(AppId) || !Utils.isUUID(AppCert))
            {
                return "";
            }

            ByteBuf buf = new ByteBuf()
                .put(Encoding.UTF8.GetBytes(AppId))
                .put(IssueTs)
                .put(Expire)
                .put(Salt)
                .put((ushort)Services.Count);

            byte[] signing = GetSign();

            foreach (var it in Services)
            {
                it.Value.Pack(buf);
            }

            byte[] signature = DynamicKeyUtil.encodeHMAC(signing, buf.asBytes(), "SHA256");

            ByteBuf bufferContent = new ByteBuf();
            bufferContent.put(signature);
            bufferContent.copy(buf.asBytes());

            return GetVersion() + Utils.base64Encode(Utils.compress(bufferContent.asBytes()));
        }

        public bool Parse(string token)
        {
            if (GetVersion().CompareTo(token.Substring(0, VersionLength)) != 0)
            {
                return false;
            }

            byte[] data = Utils.decompress(Utils.base64Decode(token.Substring(VersionLength)));

            ByteBuf buff = new ByteBuf(data);

            string signature = Encoding.UTF8.GetString(buff.readBytes());
            AppId = Encoding.UTF8.GetString(buff.readBytes());

            IssueTs = buff.readInt();
            Expire = buff.readInt();
            Salt = buff.readInt();
            short servicesNum = (short)buff.readShort();

            for (short i = 0; i < servicesNum; i++)
            {
                short serviceType = (short)buff.readShort();
                Service service = GetService(serviceType);
                service.Unpack(buff);
                Services.Add((ushort)serviceType, service);
            }

            return true;
        }

        // --- ENUMS ---
        public enum PrivilegeRtcEnum
        {
            JoinChannel = 1,
            PublishAudioStream = 2,
            PublishVideoStream = 3,
            PublishDataStream = 4
        }

        public enum PrivilegeRtmEnum { Login = 1 }
        public enum PrivilegeFpaEnum { Login = 1 }
        public enum PrivilegeChatEnum { ChatUser = 1, ChatApp = 2 }
        public enum PrivilegeApaasEnum { RoomUser = 1, User = 2, App = 3 }

        // --- SERVICES CLASSES ---
        public class Service
        {
            private short _type;
            private Dictionary<ushort, uint> _privileges = new Dictionary<ushort, uint>();

            public Service() { }

            public Service(short serviceType)
            {
                _type = serviceType;
            }

            public void AddPrivilegeRtc(PrivilegeRtcEnum privilege, uint expire) => _privileges.Add((ushort)privilege, expire);
            public void AddPrivilegeRtm(PrivilegeRtmEnum privilege, uint expire) => _privileges.Add((ushort)privilege, expire);
            public void AddPrivilegeFpa(PrivilegeFpaEnum privilege, uint expire) => _privileges.Add((ushort)privilege, expire);
            public void AddPrivilegeChat(PrivilegeChatEnum privilege, uint expire) => _privileges.Add((ushort)privilege, expire);
            public void AddPrivilegeApaas(PrivilegeApaasEnum privilege, uint expire) => _privileges.Add((ushort)privilege, expire);

            public Dictionary<ushort, uint> GetPrivileges() => _privileges;
            public short GetServiceType() => _type;
            public void SetServiceType(short type) => _type = type;

            public virtual ByteBuf Pack(ByteBuf buf)
            {
                return buf.put((ushort)_type).putIntMap(_privileges);
            }

            public virtual void Unpack(ByteBuf byteBuf)
            {
                _privileges = byteBuf.readIntMap();
            }
        }

        public class ServiceRtc : Service
        {
            public string ChannelName { get; set; } = "";
            public string Uid { get; set; } = "";

            public ServiceRtc() { SetServiceType(ServiceTypeRtc); }

            public ServiceRtc(string channelName, string uid)
            {
                SetServiceType(ServiceTypeRtc);
                ChannelName = channelName;
                Uid = uid;
            }

            public override ByteBuf Pack(ByteBuf buf)
            {
                return base.Pack(buf).put(Encoding.UTF8.GetBytes(ChannelName)).put(Encoding.UTF8.GetBytes(Uid));
            }

            public override void Unpack(ByteBuf byteBuf)
            {
                base.Unpack(byteBuf);
                ChannelName = Encoding.UTF8.GetString(byteBuf.readBytes());
                Uid = Encoding.UTF8.GetString(byteBuf.readBytes());
            }
        }

        public class ServiceRtm : Service
        {
            public string UserId { get; set; } = "";

            public ServiceRtm() { SetServiceType(ServiceTypeRtm); }

            public ServiceRtm(string userId)
            {
                SetServiceType(ServiceTypeRtm);
                UserId = userId;
            }

            public override ByteBuf Pack(ByteBuf buf)
            {
                return base.Pack(buf).put(Encoding.UTF8.GetBytes(UserId));
            }

            public override void Unpack(ByteBuf byteBuf)
            {
                base.Unpack(byteBuf);
                UserId = Encoding.UTF8.GetString(byteBuf.readBytes());
            }
        }

        public class ServiceFpa : Service
        {
            public ServiceFpa() { SetServiceType(ServiceTypeFpa); }

            public new ByteBuf Pack(ByteBuf buf) => base.Pack(buf);
            public new void Unpack(ByteBuf byteBuf) => base.Unpack(byteBuf);
        }

        public class ServiceChat : Service
        {
            public string UserId { get; set; } = "";

            public ServiceChat()
            {
                SetServiceType(ServiceTypeChat);
                UserId = "";
            }

            public ServiceChat(string userId)
            {
                SetServiceType(ServiceTypeChat);
                UserId = userId;
            }

            public override ByteBuf Pack(ByteBuf buf)
            {
                return base.Pack(buf).put(Encoding.UTF8.GetBytes(UserId));
            }

            public override void Unpack(ByteBuf byteBuf)
            {
                base.Unpack(byteBuf);
                UserId = Encoding.UTF8.GetString(byteBuf.readBytes());
            }
        }

        public class ServiceApaas : Service
        {
            public string RoomUuid { get; set; } = "";
            public string UserUuid { get; set; } = "";
            public short Role { get; set; }

            public ServiceApaas()
            {
                SetServiceType(ServiceTypeApaas);
                RoomUuid = "";
                UserUuid = "";
                Role = -1;
            }

            public ServiceApaas(string roomUuid, string userUuid, short role)
            {
                SetServiceType(ServiceTypeApaas);
                RoomUuid = roomUuid;
                UserUuid = userUuid;
                Role = role;
            }

            public ServiceApaas(string userUuid)
            {
                SetServiceType(ServiceTypeApaas);
                RoomUuid = "";
                UserUuid = userUuid;
                Role = -1;
            }

            public override ByteBuf Pack(ByteBuf buf)
            {
                return base.Pack(buf)
                    .put(Encoding.UTF8.GetBytes(RoomUuid))
                    .put(Encoding.UTF8.GetBytes(UserUuid))
                    .put((ushort)Role);
            }

            public override void Unpack(ByteBuf byteBuf)
            {
                base.Unpack(byteBuf);
                RoomUuid = Encoding.UTF8.GetString(byteBuf.readBytes());
                UserUuid = Encoding.UTF8.GetString(byteBuf.readBytes());
                Role = (short)byteBuf.readShort();
            }
        }
    }
}