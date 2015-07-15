using System;
using System.Threading;

namespace Tekitou
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Starting up websocket...");
			Thread thread = null;
			WebsocketController ws = new WebsocketController ();
			ThreadStart ts = new ThreadStart (() => {
				ws.Connect ((str) => {
					Console.WriteLine ("OPEN");
					ws.Send ("Message 1");
				}, (str2) => {
					Console.WriteLine ("Received");
					//ws.Send ("Message n");
				}, (str3)=>{ Console.WriteLine(str3); if(thread != null) thread.Join();}, 
				(str4)=>{ Console.WriteLine(str4); if(thread != null) thread.Join();});
			});

			thread = new Thread (ts);
			thread.Start ();
			while (true) {
				string s = Console.ReadLine ();
				if (!string.IsNullOrEmpty (s))
					ws.Send (s);
			}
		}
	}
}
