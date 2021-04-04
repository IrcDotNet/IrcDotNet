using System.Net.Sockets;

namespace IrcDotNet
{
    public class SyntheticSocketAsyncEventArgs : SocketAsyncEventArgs
    {
        public new int BytesTransferred { get; set; }
    }
}
