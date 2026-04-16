namespace SFARS.Infrastructure.Helpers.Agora
{
    public interface IPackable
    {
        ByteBuf marshal(ByteBuf outBuf);
    }
}