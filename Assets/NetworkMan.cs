﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using UnityEditor;

public class NetworkMan : MonoBehaviour
{
    [SerializeField]
    float speed = 10;

    public UdpClient udp;
    // Start is called before the first frame update
    void Start()
    {
        udp = new UdpClient();
       
        // Add real server IP
        udp.Connect("3.97.25.11", 12345);

        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
      
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1.0f/(30));

    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        DROP_CLIENT
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
    }

    [Serializable]
    public class Player {
        [Serializable]
        public struct receivedColor {
            public float R;
            public float G;
            public float B;
        }
        [Serializable]
        public struct receivecPosition{
            public float x;
            public float y;
            public float z;
        }
        public string id;
        public receivedColor color;
        public receivecPosition position;
        public int identity = 0;
    }

    [Serializable]
    public class NewPlayer{
        public Player[] player;
    }

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    // Add GameObjectPlayers Class
    [Serializable]
    public class GameObjectPlayers{
        public Player player;
        public GameObject cube;
    }

    // Add DropPlayer Class
    [Serializable]
    public class DropPlayer{
        public Player droppedPlayer;
    }

    public Message latestMessage;
    public GameState lastestGameState;
    public NewPlayer latestPlayer;
    public DropPlayer dropPlayer;

    List<Player> connectedPlayers = new List<Player>();
    List<GameObjectPlayers> gameObjectPlayers = new List<GameObjectPlayers>();
    Player currentPlayer = new Player();

    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        Debug.Log("Got this: " + returnData);


        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        
        try
        {
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    // Add the details of new player into a list of currently connected players
                    latestPlayer = JsonUtility.FromJson<NewPlayer>(returnData);
                    for(int i = 0; i < latestPlayer.player.Length; i++)
                    {
                        bool hasNew = true;
                        foreach (Player p in connectedPlayers)
                        {
                            if (latestPlayer.player[i].id == p.id)
                            {
                                hasNew = false;
                                break;
                            }                               
                        }
                        if(hasNew == true)
                        {
                            connectedPlayers.Add(latestPlayer.player[i]);
                        }

                        if(currentPlayer.identity == 0)
                        {
                            if(latestPlayer.player[i].identity == 1)
                            {
                                currentPlayer.id = latestPlayer.player[i].id;
                                currentPlayer.identity = 1;
                            }
                        }

                    }
                    
                    break;
                case commands.UPDATE:
                    lastestGameState = JsonUtility.FromJson<GameState>(returnData);
                    break;
                case commands.DROP_CLIENT:
                    // Remove the player’s entry from the list of currently connected players
                    dropPlayer = JsonUtility.FromJson<DropPlayer>(returnData);
                    for(int i = 0; i < connectedPlayers.Count; i ++)
                    {
                        if(dropPlayer.droppedPlayer.id == connectedPlayers[i].id)
                        {
                            connectedPlayers.RemoveAt(i);
                        }
                    }
                    break;
                default:
                    Debug.Log("Error");
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers(){
        // When a new player is connected, the client spawns a cube to represent the newly connected player
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            bool hasNew = true;
            foreach (GameObjectPlayers gO in gameObjectPlayers)
            {
                if (connectedPlayers[i].id == gO.player.id)
                {
                    hasNew = false;
                    break;
                }
            }
            if (hasNew == true)
            {
                gameObjectPlayers.Add(new GameObjectPlayers() { player = connectedPlayers[i], cube = GameObject.CreatePrimitive(PrimitiveType.Cube) });
                gameObjectPlayers[gameObjectPlayers.Count - 1].cube.transform.position = new Vector3(-2.5f + (gameObjectPlayers.Count - 1) * 2.5f, 2.5f, 0.5f + (gameObjectPlayers.Count - 1) * 2.5f);
            }
        }

        foreach(GameObjectPlayers gO in gameObjectPlayers)
        {
            if(gO.player.id == currentPlayer.id)
            {
                currentPlayer.position.x = gO.cube.transform.position.x;
                currentPlayer.position.y = gO.cube.transform.position.y;
                currentPlayer.position.z = gO.cube.transform.position.z;
            }
        }
    }

    void UpdatePlayers(){
        // The client loops through all the currently connected players and updates the player game object properties
        foreach (GameObjectPlayers gO in gameObjectPlayers)
        {
            if (gO.player.id == currentPlayer.id)
            {
                currentPlayer.position.x = gO.cube.transform.position.x;
                currentPlayer.position.y = gO.cube.transform.position.y;
                currentPlayer.position.z = gO.cube.transform.position.z;
            }
        }
        foreach (GameObjectPlayers gO in gameObjectPlayers)
        {
            foreach(Player p in lastestGameState.players)
            {
                if (gO.player.id == p.id)
                {
                    gO.player.color = p.color;
                    gO.cube.GetComponent<Renderer>().material.color = new Color(p.color.R, p.color.G, p.color.B);
                    if(gO.player.id != currentPlayer.id)
                    {
                        gO.cube.transform.position = new Vector3(p.position.x, p.position.y, p.position.z);
                    }

                }
            }
        }
    }

    void DestroyPlayers()
    {

        // When a player is dropped, the client destroys the player’s game object
        if (gameObjectPlayers.Count > 0)
        {
            for(int i = 0; i < gameObjectPlayers.Count; i++)
            {
                if (dropPlayer.droppedPlayer.id == gameObjectPlayers[i].player.id)
                {
                    Destroy(gameObjectPlayers[i].cube);
                    gameObjectPlayers.RemoveAt(i);
                }
            }
        }

        Debug.Log("Test: " + gameObjectPlayers.Count);
    }
    
    void HeartBeat(){
        // Periodically send the position of the cube owned by the player
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat " + currentPlayer.position.x + " " + currentPlayer.position.y + " " + currentPlayer.position.z);
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        // Move the character object around
        Vector3 velocity = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0) * speed * Time.deltaTime;
        foreach (GameObjectPlayers gO in gameObjectPlayers)
        {
            if (gO.player.id == currentPlayer.id)
            {
                gO.cube.transform.Translate(velocity);
            }
        }
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}
