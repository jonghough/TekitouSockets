using System;
using System.Net.Sockets;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace Tekitou
{
	/// <summary>
	/// Websocket controller.
	/// </summary>
	public class WebsocketController
	{
		//Globally unique identifier, see RFC6454
		public const string GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
		const int NORMAL_CLOSURE = 1000;
		const int GOING_AWAY = 1001;
		const int PROTOCOL_ERROR = 1002;
		const int UNSUPPORTED_DATA = 1003;
		const int RESERVED = 1004;
		const int NO_STATUS_RECEIVED = 1005;
		const int ABNORMAl_CLOSURE = 1006;
		const int INVALID_DATA = 1007;
		const int POLICY_VIOLATION = 1008;
		const int MESSAGE_TOO_BIG = 1009;
		const int MANDATORY_EXT = 1010;
		const int INTERNAL_SERVER_ERR = 1011;
		const int TLS_HANDSHAKE = 1015;
		//Websocket op-codes
		//see RFC 6455 Section 11.8;// (Opcodes)
		const int CONTINUATION_FRAME = 0x0;
		const int TEXT_FRAME = 0x1;
		const int BINARY_FRAME = 0x2;
		const int CLOSE_FRAME = 0x8;
		const int PING_FRAME = 0x9;
		const int PONG_FRAME = 0xA;
		//Frame bits
		const int fin_bits = 0x80;
		const int res1_bits = 0x40;
		const int res2_bits = 0x20;
		const int res3_bits = 0x10;
		const int opcode = 0xF;
		//opcode
		const int len_mask = 0x80;
		const int MAX_DATA_NO_EXTENSION = 126;
		const int DATA_2_BYTE_EXTENSION = 1 << 16;
		const int DATA_8_BYTE_EXTENSION = 1 << 63;

		private enum State
		{ 			
			CONNECTING,
			HANDSHAKE,
			CONNECTED,
			CLOSING,
			CLOSED
		}

		private State _state = State.CLOSED;
		private bool _continuationFrameFlg = false;
		private byte[] _continuationData;
		string _protocol = "ws";
		string _url = "";
		string _port = "80";
		string _resource = "/";
		string _hostUri = "";
		static byte[] data = new byte[1024];
		private Action<string> _onReceive;
		private Action<string> _onError;
		private Action<string> _onOpen;
		private Action<string> _onClose;
		private Socket _server;

		public void Setup (string url, string port, string resource = null, string hostUri = null)
		{
			_url = url.Split ("://".ToCharArray ()) [3].Trim ();
			_port = port;
			if(resource != null)
				_resource = resource;
			if (hostUri != null)
				_hostUri = hostUri;
			_protocol = url.Split ("://".ToCharArray ()) [0].Trim (); //ws or wss
			_state = State.CLOSED;

		}

		public void Connect (Action<string> onOpen, Action<string> onReceive, Action<string> onClose, Action<string> onError)
		{
			_onOpen = onOpen;
			_onReceive = onReceive;
			_onClose = onClose;
			_onError = onError;

			_state = State.CONNECTING;

			_server = new Socket (AddressFamily.InterNetwork,
			                      SocketType.Stream, ProtocolType.Tcp);

			_server.Connect (_url, int.Parse(_port));
			string hk = GenerateHeaderKey ();
			string hdr = MakeHeader (hk, _hostUri, _port, _resource, "null");
			Console.WriteLine (hdr);
			_server.Send (Encoding.UTF8.GetBytes (hdr));


			int recv = _server.Receive (data);
			string stringData = Encoding.ASCII.GetString (data, 0, recv);
			_state = State.HANDSHAKE;
			Console.WriteLine (stringData);
			string[] stringArr = stringData.Split ('\n');
			Dictionary<string, string> dic = new Dictionary<string, string> ();
			foreach (string s in stringArr) {
				Console.WriteLine ("line::=>  " + s);
				string[] s2 = s.Split (':');
				if (s2.Length > 1) {
					dic [s2 [0].Trim ()] = s2 [1].Trim ();
				}
			}

			if (ExpectedValue (hk).Trim () == dic ["Sec-WebSocket-Accept"]) {
				_state = State.CONNECTED;
				if (_onOpen != null)
					_onOpen ("open");

				Listen (_server);
			} else {
				_server.Close ();
				_onError ("Handshake failed.");
				_state = State.CLOSED;
			}
		}

		public void Close (string reason)
		{
			if (_server != null && (_state == State.CONNECTED || _state == State.CONNECTING)) {
				_server.Send (MakeFrame (reason, CLOSE_FRAME));
				_state = State.CLOSED;
			}
		}

		private void Listen (Socket server)
		{
			int recv = 0;
			string stringData;
			while (_state == State.CONNECTED || _state == State.CLOSING) {
				recv = server.Receive (data);
				stringData = Encoding.ASCII.GetString (data, 0, recv);
				FrameHolder f = new FrameHolder (data);
				//Console.WriteLine (f._finbit + ", " + Encoding.ASCII.GetString (f._message) + ", " + f._opcode);
				//begin fragment
				if (f._finbit == 0 && !_continuationFrameFlg && f._opcode == CONTINUATION_FRAME) {
					_continuationFrameFlg = true;
					_continuationData = f._message;
				} else {
					//switch opcode
					switch (f._opcode) {
						case TEXT_FRAME:
					{
						if (_onReceive != null) {
							_onReceive (Encoding.ASCII.GetString (f._message));
						}
						break;
					}
						case BINARY_FRAME:
						break;
						case CONTINUATION_FRAME:
						_continuationFrameFlg = true;

						if (_continuationData == null)
							_continuationData = f._message;
						else
							_continuationData = Combine (_continuationData, f._message);

						if (f._finbit == 1) {
							_continuationFrameFlg = false;
							_onReceive (Encoding.UTF8.GetString (_continuationData));
							_continuationData = null;
						}
						break;
						case CLOSE_FRAME:
						_server.Send (MakeFrame ("1000", CLOSE_FRAME));
						_state = State.CLOSED;
						_onClose ("Connection closed.");
						break;
						case PING_FRAME:
						_server.Send (MakeFrame (f._message, PONG_FRAME));
						break;
						case PONG_FRAME:
						_server.Send (MakeFrame (f._message, PING_FRAME));
						break;
					}
				}
			}
		}

		public static string MakeHeader (string socketKey, string hostUri, string port, string resource, string origin)
		{
			string header = "GET " + resource + " HTTP/1.1\r\n"
				+ "Upgrade: websocket\r\n"
					+ "Connection: Upgrade\r\n"
					+ "Host: " + hostUri + "\r\n"
					+ "Origin: " + origin + " \r\n"
					+ "Sec-WebSocket-Key: " + socketKey + "\r\n"
					+ "Sec-WebSocket-Protocol: chat, superchat\r\n"
					+ "Sec-WebSocket-Version: 13\r\n\r\n";
			return header;
		}

		public static string GenerateHeaderKey ()
		{
			byte[] array = new byte[16];
			Random random = new Random ();
			random.NextBytes (array);
			return Convert.ToBase64String (array);
		}

		public static string ExpectedValue (string v)
		{
			HashAlgorithm algorithm = SHA1.Create ();  
			return Convert.ToBase64String (algorithm.ComputeHash (Encoding.UTF8.GetBytes (v + GUID)));
		}

		public string ProcessResponse (byte[] res)
		{
			FrameHolder fh = new FrameHolder (res);
			// for now
			return fh.msg;

		}

		public void Send (string message)
		{
			byte[] fr = MakeFrame (message, TEXT_FRAME);
			_server.Send (fr);
		}

		public static byte[] MakeFrame (string message, int opcode)
		{
			byte[] data = Encoding.UTF8.GetBytes (message);
			return MakeFrame (data, opcode);
		}

		public static byte[] MakeFrame (byte[] data, int opcode)
		{
			int frameInt = (1 << 7) | opcode;
			int maskBit = 1 << 7;
			int len = data.Length;
			byte[] frame;
			if (len < MAX_DATA_NO_EXTENSION)
				frame = new byte[] {
				BitConverter.GetBytes (frameInt) [0],
				BitConverter.GetBytes (maskBit | len) [0]
			};
			else if (len < DATA_2_BYTE_EXTENSION)
				frame = Combine (BitConverter.GetBytes (maskBit | 0x7e), BitConverter.GetBytes (len));
			else
				frame = Combine (BitConverter.GetBytes (maskBit | 0x7f), BitConverter.GetBytes (len));
			byte[] k = new byte[4];
			Random random = new Random ();
			random.NextBytes (k);
			byte[] res = Combine (Combine (frame, k), Mask (k, data));
			return res;
		}

		public static byte[] Combine (byte[] first, byte[] second)
		{
			byte[] ret = new byte[first.Length + second.Length];
			Buffer.BlockCopy (first, 0, ret, 0, first.Length);
			Buffer.BlockCopy (second, 0, ret, first.Length, second.Length);
			return ret;
		}

		public static byte[] Mask (byte[] key, byte[] data)
		{
			for (int i = 0; i < data.Length; i++) {
				data [i] ^= key [i % 4];
			}
			return data;
		}
	}
}
public class FrameHolder
{
	public bool _valid_frame = true;
	public byte _finbit = 0;
	public byte _opcode = 0;
	public int _msg_length = 0;
	public byte[] _message = null;
	public string msg = null;

	public FrameHolder (byte[] rawFrame)
	{
		//this.FrameHolder ();
		byte first = rawFrame [0];
		_finbit = (byte)((first >> 7) & 0xFF);
		_opcode = (byte)(first & 0xF);

		byte l = rawFrame [1];
		_msg_length = (l & 0xFF);

		if (l == 126) {
			_message = Slice (rawFrame, 4, _msg_length);
		} else if (l == 127) {
			_message = Slice (rawFrame, 10, _msg_length);
		} else {
			_message = Slice (rawFrame, 2, _msg_length);
		}

		msg = Encoding.UTF8.GetString (_message);
	}

	/// <summary>
	/// Slice the source byte array by offset and length.
	/// </summary>
	/// <param name="src">Source.</param>
	/// <param name="offset">Offset.</param>
	/// <param name="length">Length.</param>
	byte[] Slice (byte[] src, int offset, int length)
	{
		byte[] dst = new byte[length];
		Array.Copy (src, offset, dst, 0, length);
		return dst;
	}
}