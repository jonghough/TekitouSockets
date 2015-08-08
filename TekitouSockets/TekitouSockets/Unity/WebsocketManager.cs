using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using Tekitou;
/// <summary>
/// Websocket manager. Controls the Websocket connection.
/// </summary>
public class WebsocketManager : MonoBehaviour {

	private WebsocketController _websocketController;
	private Thread _thread;
	private ThreadStart _threadStart;
	private object _lock = new object();
	public Action<string> _onReceive, _onOpen, _onError, _onClose;
	private string _recStr, _openStr, _errStr, _closeStr;
	// Use this for initialization
	void Start () {
		
		/******************************************************
		 *           ~~~--- EXAMPLE USAGE ---~~~
		 * The following is just an example of how to use the
		 * WebsocketController. Define the 4 methods _onOpen,
		 * _onReceive, _onClose, _onError.
		 * In this example, we just output the received string
		 * in the console log. 
		 * For the example, try to connect to websocket.org's
		 * echo server and send some echos back and forth to the 
		 * server forever.
		 * ***************************************************/
		_onReceive = (s) => {
			Debug.Log ("Received " + s);
			_websocketController.Send ("Sending echo message forever.");
		};
		_onOpen = (s) => {
			Debug.Log ("Connection opened " + s); 
			_websocketController.Send ("Sending echo message.");
		};
		_onClose = (s) => Debug.Log ("Connection closed, " + s);
		_onError = (s) => Debug.Log ("Connection error, " + s);
		//connect to websocket.org to do echo test.
		Setup ("ws://websocket.org", "80", "/echo", "echo.websocket.org");
	}

	void OnDestroy(){
		//destroy the websocket connection.
		if (_websocketController != null) {
			_websocketController.Close ("closing");
			_thread = null;
		}

	}
	
	// Update is called once per frame
	void Update () {
		lock (_lock) {
			if (_recStr != null) {
				if (_onReceive != null) {
					_onReceive (_recStr);
					_recStr = null;
				}
			}
		}
		lock (_lock) {
			if (_openStr != null) {
				if (_onOpen != null) {
					_onOpen (_openStr);
					_openStr = null;
				}
			}
		}
		lock (_lock) {
			if (_errStr != null) {
				if (_onError != null) {
					_onError (_errStr);
					_errStr = null;
				}
			}
		}
		lock (_lock) {
			if (_closeStr != null) {
				if (_onClose != null) {
					_onClose (_closeStr);
					_closeStr = null;
				}
			}
		}
	}

	/// <summary>
	/// Connection state.
	/// </summary>
	/// <returns>The state.</returns>
	public WebsocketController.State ConnectionState(){
		lock (_lock) {
			if(_websocketController == null )
				throw new Exception("Websocket controller is not instantiated.");
			else return _websocketController.GetConnectionState();
		}
	}

	/// <summary>
	/// Sends string message to the server, if server is not null and
	/// connection is established. Otherwise will return false.
	/// </summary>
	/// <param name="msg">Message.</param>
	public bool Send(string msg){
		lock (_lock) {
			if(string.IsNullOrEmpty(msg)) return false;
			if(_websocketController == null) return false;
			if(_websocketController.GetConnectionState() != WebsocketController.State.CONNECTED){
				return false;
			}
			else{
				_websocketController.Send(msg);
				return true;
			}
		}
	}

	/// <summary>
	/// Sets up the Websocekt connection with the specified url, port, resource and hostUri.
	/// Intializes connection with server.
	/// </summary>
	/// <param name="url">URL.</param>
	/// <param name="port">Port.</param>
	/// <param name="resource">Resource.</param>
	/// <param name="hostUri">Host URI.</param>
	public void Setup(string url, string port = "80", string resource = null, string hostUri = null){
		lock (_lock) {
			_thread = null;

			_websocketController = new WebsocketController ();
			_websocketController.Setup (url, port, resource, hostUri);

			_threadStart = new ThreadStart( ()=>{
				_websocketController.Connect (
					(o) => {lock(_lock){_openStr = o;}},
					(r) => {lock(_lock){_recStr = r;}},
					(c) => {lock(_lock){_closeStr = c;}},
					(e) => {lock(_lock){_errStr = e;}}
				);
			});
			_thread = new Thread (_threadStart);
			_thread.Start ();
		}
	}

	///<summary>
	/// Close the websocket connection if it is open.
	///</summary>
	public void Close(){
		lock (_lock) {
			if(_websocketController != null)
				_websocketController.Close();
		}
	}
}
