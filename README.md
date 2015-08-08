# TekitouSockets
適当なwebsockets

This is a quick, simple and dirty implementation of a websocket client written in C#. It was originally developed to do some quick network game prototyping for Unity3d. 

##Dependencies
* Must be able to use .Net sockets (Unity 5.x+ allows handheld games to use System.Sockets even with the free version of Unity)
* .Net 3.5+
* It was developed and tested with Mono, so should work no problem.

##Usage
* clone the repo
* Drop WebsocketController.cs in a  VS / Mono / Unity3d project.

##Unity3d
* There is an example MonoBehaviour script, `WebsocketManager.cs` , that can be attached to some Unity GameObject and used with `WebsocketController.cs` to use Websockets with Unity.


## Example Unity usage 
(see `WebsocketManager.cs` )

The following just creates an infinite loop of echos with an echo server.
```
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
```



