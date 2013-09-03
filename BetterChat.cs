using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using JsonFx.Json;
using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;
using Irrelevant.Assets;

namespace UserMenuInChat.mod
{


    using System;
    using UnityEngine;


    public class BetterChat : BaseMod, ICommListener, iEffect, iCardRule
    {
        struct logentry 
        {
            public string msg;
            public string from;
            public Rect userrec;
            public int end;//y-coordinate, where msg ends
            public int usernamelength;
            public bool admin;
        }

        struct wordclickdata
        {
            public Rect[] wordrects;
            public string lastline;
        
        }

        struct cardtextures
        {
        public Texture cardimgimg;
        public Rect cardimgrec;
        }

        struct renderwords
        {
            public string text;
            public GUIStyle style;
            public Rect rect;
            public Color color;
        }

        struct cardsearcher 
        {
            public string clickedcardname;
            public bool success;
        }

        private int clickedwordindex;
        private string[] clickedtext=new string[0];
        private int longestcardname;

        private List<renderwords> textsArr = new List<renderwords>();
        private List<cardtextures> gameObjects = new List<cardtextures>();
        private FieldInfo icoField;
        private FieldInfo statsBGField;
        private FieldInfo icoBGField;
        private FieldInfo gosNumHitPointsField;
        private FieldInfo gosNumAttackPowerField;
        private FieldInfo gosNumCountdownField;
        private FieldInfo gosNumCostField;
        private FieldInfo gosactiveAbilityField;
        private FieldInfo textsArrField;
        private FieldInfo cardImageField;

        int screenh = 0;
        int screenw = 0;
        float scalefactor=1.0f;
        Camera cam=new Camera();
        Texture cardtext;
        Rect cardrect;
        bool clicked = false;
        string currentroom="";
        int posycopy = 0;
        bool recalc = false;
        private List<logentry> roomuserlist= new List<logentry>();
        private const bool debug = false;
        string globallink = "";

        private FieldInfo chatScrollinfo;
        private FieldInfo allowSendingChallengesinfo; 
        private FieldInfo userContextMenuinfo ;
        private FieldInfo maxScrollinfo;
        private FieldInfo chatlogAreaInnerinfo;
        private FieldInfo chatRoomsinfo;
        private FieldInfo timeStampStyleinfo;
        private FieldInfo chatLogStyleinfo;
        private MethodInfo CloseUserMenuinfo;
        private MethodInfo createUserMenuinfo;
        private MethodInfo challengeUserMethod;    
        private MethodInfo profileUserMethod;
        private MethodInfo tradeUserMethod;
        private FieldInfo userContextMenuField;
        private ChatUI target = null;
        private ChatRooms chatRooms;
        private GUIStyle timeStampStyle;
        private GUIStyle chatLogStyle;
        private MethodInfo getchatlogMethod;
        //private MethodInfo createUserMenu;
        private Regex userRegex;
        private Regex linkFinder;
        private Regex cardlinkfinder;

        // dict from room log, to another dict that maps chatline to a username
        //dont use currently
        private Dictionary<RoomLog, Dictionary<RoomLog.ChatLine, string>> chatLineToUserNameCache = new Dictionary<RoomLog, Dictionary<RoomLog.ChatLine, string>>();
       
        // dict from room name, to another dict that maps username to ChatUser
        // dont use currently
        private Dictionary<string, Dictionary<string, ChatUser>> userNameToUserCache = new Dictionary<string, Dictionary<string, ChatUser>>();
        private Dictionary<string, ChatUser> globalusers = new Dictionary<string, ChatUser>();
        private int[] cardids;
        private string[] cardnames;
        private int[] cardImageid;
        private string[] cardType;

        private GameObject cardRule;
        private GameObject GUIObject;
        bool mytext=false;
        //CardOverlay cardOverlay;

        private int cardnametoid(string name) { return cardids[Array.FindIndex(cardnames, element => element.Equals(name))]; }
        private int cardnametoimageid(string name) { return cardImageid[Array.FindIndex(cardnames, element => element.Equals(name))]; }

        public void handleMessage(Message msg)
        { // collect data for enchantments (or units who buff)

            if (msg is CardTypesMessage)
            {
                
                JsonReader jsonReader = new JsonReader();
                Dictionary<string, object> dictionary = (Dictionary<string, object>)jsonReader.Read(msg.getRawText());
                Dictionary<string, object>[] d = (Dictionary<string, object>[])dictionary["cardTypes"];
                this.cardids = new int[d.GetLength(0)];
                this.cardnames = new string[d.GetLength(0)];
                this.cardImageid = new int[d.GetLength(0)];
                this.cardType = new string[d.GetLength(0)];
                this.longestcardname = 0;
                for (int i = 0; i < d.GetLength(0); i++)
                {
                    cardids[i] = Convert.ToInt32(d[i]["id"]);
                    cardnames[i] = d[i]["name"].ToString().ToLower();
                    cardImageid[i] = Convert.ToInt32(d[i]["cardImage"]);
                    cardType[i] = d[i]["kind"].ToString();
                    if (cardnames[i].Split(' ').Length > longestcardname) { longestcardname = cardnames[i].Split(' ').Length; };

                }
                App.Communicator.removeListener(this);//dont need the listener anymore
            }

            return;
        }
        public void onReconnect()
        {
            return; // don't care
        }



        public BetterChat()
        {
            // match until first instance of ':' (finds the username)
            userRegex = new Regex(@"[^:]*"
                /*, RegexOptions.Compiled*/); // the version of Mono used by Scrolls version of Unity does not support compiled regexes
            // from http://daringfireball.net/2010/07/improved_regex_for_matching_urls
            // I had to remove a " in there to make it work, but it should match well enough anyway
            linkFinder = new Regex(@"(?i)\b((?:[a-z][\w-]+:(?:/{1,3}|[a-z0-9%])|www\d{0,3}[.]|[a-z0-9.\-]+[.][a-z]{2,4}/)(?:[^\s()<>]+|\(([^\s()<>]+|(\([^\s()<>]+\)))*\))+(?:\(([^\s()<>]+|(\([^\s()<>]+\)))*\)|[^\s`!()\[\]{};:'.,<>?«»“”‘’]))"
                /*, RegexOptions.Compiled*/);
            cardlinkfinder = new Regex(@"\[[a-zA-Z]+[a-zA-Z_\t]*[a-zA-z]+\]");//search for "[blub_blub_blub]"


            statsBGField = typeof(CardView).GetField("statsBG", BindingFlags.Instance | BindingFlags.NonPublic);
            icoBGField = typeof(CardView).GetField("icoBG", BindingFlags.Instance | BindingFlags.NonPublic);
            icoField = typeof(CardView).GetField("ico", BindingFlags.Instance | BindingFlags.NonPublic);
            gosNumAttackPowerField = typeof(CardView).GetField("gosNumAttackPower", BindingFlags.Instance | BindingFlags.NonPublic);
            gosNumCountdownField = typeof(CardView).GetField("gosNumCountdown", BindingFlags.Instance | BindingFlags.NonPublic);
            gosNumCostField = typeof(CardView).GetField("gosNumCost", BindingFlags.Instance | BindingFlags.NonPublic);
            gosNumHitPointsField = typeof(CardView).GetField("gosNumHitPoints", BindingFlags.Instance | BindingFlags.NonPublic);
            textsArrField = typeof(CardView).GetField("textsArr", BindingFlags.Instance | BindingFlags.NonPublic);
            cardImageField = typeof(CardView).GetField("cardImage", BindingFlags.Instance | BindingFlags.NonPublic);
            gosactiveAbilityField = typeof(CardView).GetField("gosActiveAbilities", BindingFlags.Instance | BindingFlags.NonPublic);

            CloseUserMenuinfo = typeof(ChatUI).GetMethod("CloseUserMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            createUserMenuinfo = typeof(ChatUI).GetMethod("CreateUserMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            challengeUserMethod = typeof(ChatUI).GetMethod("ChallengeUser", BindingFlags.Instance | BindingFlags.NonPublic);
            tradeUserMethod = typeof(ChatUI).GetMethod("TradeUser", BindingFlags.Instance | BindingFlags.NonPublic);
            profileUserMethod = typeof(ChatUI).GetMethod("ProfileUser", BindingFlags.Instance | BindingFlags.NonPublic);
            getchatlogMethod = typeof(ChatRooms).GetMethod("GetChatLog", BindingFlags.Instance | BindingFlags.NonPublic);

            userContextMenuField = typeof(ChatUI).GetField("userContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            chatRoomsinfo = typeof(ChatUI).GetField("chatRooms", BindingFlags.Instance | BindingFlags.NonPublic);
            timeStampStyleinfo = typeof(ChatUI).GetField("timeStampStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            chatLogStyleinfo = typeof(ChatUI).GetField("chatLogStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            chatlogAreaInnerinfo = typeof(ChatUI).GetField("chatlogAreaInner", BindingFlags.Instance | BindingFlags.NonPublic);
            chatScrollinfo = typeof(ChatUI).GetField("chatScroll", BindingFlags.Instance | BindingFlags.NonPublic);
            allowSendingChallengesinfo = typeof(ChatUI).GetField("allowSendingChallenges", BindingFlags.Instance | BindingFlags.NonPublic);
            userContextMenuinfo = typeof(ChatUI).GetField("userContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            maxScrollinfo = typeof(ChatUI).GetField("maxScroll", BindingFlags.Instance | BindingFlags.NonPublic);

            this.GUIObject = new GameObject();
            this.GUIObject.transform.parent = Camera.main.transform;
            this.GUIObject.transform.localPosition = new Vector3(0f, 0f, Camera.main.transform.position.y - 0.3f);

            


            try
            {
                App.Communicator.addListener(this);
            }
            catch { }

        }

        public static string GetName()
        {
            return "Betterchat";
        }

        public static int GetVersion()
        {
            return 3;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
        {
            try
            {
                return new MethodDefinition[] {
                    scrollsTypes["ChatRooms"].Methods.GetMethod("LeaveRoom", new Type[]{typeof(string)}),
                    scrollsTypes["ChatRooms"].Methods.GetMethod("SetRoomInfo", new Type[] {typeof(RoomInfoMessage)}),
                    scrollsTypes["ChatUI"].Methods.GetMethod("OnGUI")[0],
                    //scrollsTypes["ChatRooms"].Methods.GetMethod("ChatMessage", new Type[]{typeof(RoomChatMessageMessage)}),
                   scrollsTypes["ArenaChat"].Methods.GetMethod("handleMessage", new Type[]{typeof(Message)}),


                    // only for testing:
                    //scrollsTypes["ChatRooms"].Methods.GetMethod("GetUserAdminRoleInRoom", new Type[]{typeof(string),typeof(string)}),
                    //scrollsTypes["Communicator"].Methods.GetMethod("sendRequest", new Type[]{typeof(Message)}),  
                };
            }
            catch
            {
                return new MethodDefinition[] { };
            }
        }


        public override bool WantsToReplace(InvocationInfo info)
        {
            /*if (info.target is ChatRooms && info.targetMethod.Equals("GetUserAdminRoleInRoom"))
            {
               string name = (string) info.arguments[0];
               if (name == "usernameXD")
               { return true; }
            }
             */
            // for testing
            /*
            if (info.target is Communicator && info.targetMethod.Equals("sendRequest"))
            {
                
                if (info.arguments[0] is RoomChatMessageMessage)
                {


                    RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                    string[] splitt = msg.text.Split(' ');
                    if ((splitt[0] == "/test"))
                    {
                        return true;
                    }
                }
                
            }
             */ 
            return false;
        }
        public override void ReplaceMethod(InvocationInfo info, out object returnValue)
        {
            returnValue = null;
            // testing admin
            /*if (info.target is ChatRooms && info.targetMethod.Equals("GetUserAdminRoleInRoom"))
            {
                returnValue = AdminRole.Mojang; // or AdminRole.Admin
            }
            */
            // testing some messages:
            /*
            if (info.target is Communicator && info.targetMethod.Equals("sendRequest"))
            {
                //Console.WriteLine("sendrequest");
                if (info.arguments[0] is RoomChatMessageMessage)
                {


                    RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                    string[] splitt = msg.text.Split(' ');
                    if (splitt.Length == 2)
                    {

                        if (splitt[1] == "2")
                        {
                            string text = @"WTS ALL ORDER AND ENERGY*  Honorable General 520g, Unleash inner power 345g Kinfolk Vet 150. Kinfolk Jarl 350, Blessing of Haste 250, Magma Pack 130g, Faith Duty 250";
                            RoomChatMessageMessage joinmessage = new RoomChatMessageMessage(msg.roomName, text);
                            joinmessage.from = "Alecburn";
                            App.ChatUI.handleMessage(joinmessage);
                            App.ArenaChat.ChatRooms.ChatMessage(joinmessage);

                            return;
                        }
                    }

                    if ((splitt[0] == "/test"))
                    {
                        string text = @"WTS >>Redeploy (1101g) // Flip (197g) // Kabonk (78g) // Shrine (242g) // Sinmarked Zealot (118g) // Pother (719g) // Transposition (88g) // Focus (96g) // Honorable General (535g) // Powerbound (71g) // Vengeance Veil (50g) // Horn of Ages (54g) // Summons (68g) // Woodland Memorial (85g) // Knight Scholar (513g) // Ducal Infantryman (74g) // Ducal Skirmisher (89g) // Crossbowman (76g) // Royal Spearman (130g) // Efficiency (207g) // Mangonel (326g) // Divine Mark (178g) // Faith Duty (119g) // ";
                        RoomChatMessageMessage joinmessage = new RoomChatMessageMessage(msg.roomName,  text);
                        joinmessage.from = "stever2410";
                        App.ChatUI.handleMessage(joinmessage);
                        App.ArenaChat.ChatRooms.ChatMessage(joinmessage);
                    }
                }
            }
                */

        }

        public override void BeforeInvoke(InvocationInfo info)
        {
            if (info.target is ChatRooms && info.targetMethod.Equals("LeaveRoom"))
            {
                string room = (string)info.arguments[0];
                if (userNameToUserCache.ContainsKey(room))
                {
                    userNameToUserCache.Remove(room);
                }
            }

            return;
        }

        private void ChallengeUser(ChatUser user)
        {
            challengeUserMethod.Invoke(target, new object[] { user });
        }
        private void TradeUser(ChatUser user)
        {
            tradeUserMethod.Invoke(target, new object[] { user });
        }
        private void ProfileUser(ChatUser user)
        {
            profileUserMethod.Invoke(target, new object[] { user });
        }
        private void OpenLink(ChatUser user)
        {
            this.CloseUserMenuinfo.Invoke(target, null);
            Process.Start(this.globallink);
        }

        private void whisperclick(ChatUser user)
        {
            this.CloseUserMenuinfo.Invoke(target,null);
            App.ArenaChat.OpenWhisperRoom(user.name);
            
        }


        private string whitchwordclicked(string mssg, GUIStyle style, float width, int normlhight, int globalfromxstart, int ystart, Vector2 mousepos, bool admin) 
        { //calculate which word you have clicked!
            string clearmssg = Regex.Replace(mssg, @"(<color=#[A-Za-z0-9]{0,6}>)|(</color>)", String.Empty);
            if (admin)
            {
                clearmssg = "iiii "+clearmssg;// "# " has worked properly?
            }
            string[] words = clearmssg.Split(' ');
            string klickedword = "";
            string lastline = "";
            for (int i = 0; i < words.Length; i++) 
            {
                string mssgbeforeword = "";
                if (i >= 1) { mssgbeforeword = string.Join(" ", words, 0, i)+" "; }
                //string mssgafterword = string.Join(" ", words, 0, i);
                wordclickdata data = wordlocator(words[i], mssgbeforeword, style, width, normlhight, globalfromxstart, ystart, lastline);
                Rect[] rects = data.wordrects;
                lastline = data.lastline;
                bool clicked = false;
                Rect lastrec=new Rect(0,0,0,0);
                foreach (Rect rec in rects)
                {

                    clicked = clicked || rec.Contains(mousepos);
                    lastrec = rec;
                }
                if (clicked) { klickedword = words[i]; this.clickedwordindex = i; clickedtext = words; break; }
                if (mousepos.y < lastrec.yMin) {break; };           
            }
            if (!(klickedword == "")) { Console.WriteLine("### KLICKED: "+klickedword); }

            
            return klickedword;
        }

        private wordclickdata wordlocator(string link, string mssg, GUIStyle style, float width, int normlhight, int globalfromxstart, int ystart,string lastline)
        {
            style.wordWrap = true;
            string txttilllink = mssg;
            
            string txtwithlink = txttilllink + link;
            int linkbeginhight = (int)style.CalcHeight(new GUIContent(txttilllink), width);
            int temp = (int)style.CalcHeight(new GUIContent(txtwithlink), width);
            bool startnewline=false;
            if (temp > linkbeginhight) { linkbeginhight = linkbeginhight + normlhight;startnewline=true; } // if the link is too long, he will start on an extra line
            int ind = 0;


            string linetilllinkstart = lastline + " ";
            if (startnewline) { linetilllinkstart = ""; };
            ind = 0;
            int linkendhight = (int)style.CalcHeight(new GUIContent(txtwithlink), width);
            int linkbeginnindex = txttilllink.Length;
            string linetilllinkend = linetilllinkstart + link;// we need this for calculating the x-coordinate of link-ending
            if (!(linkbeginhight == linkendhight))      // if the end of the link isnt in the same line like the begining, we have to find the line where it ends 
            {
                for (int i = link.Length; i > 0; i--)
                {
                    int tmphight = (int)style.CalcHeight(new GUIContent(txtwithlink.Substring(0, linkbeginnindex + i)), width);
                    //Console.WriteLine(txtwithlink.Substring(linkbeginnindex, i) + tmphight);
                    if (tmphight < linkendhight) { ind = i + 1; break; };
                }
                linetilllinkend = txtwithlink.Substring(linkbeginnindex + ind - 1, (link.Length) - ind);
            }
            //Console.WriteLine("Linksearcher:#" + linetilllinkstart + "#and#" + linetilllinkend+"#");

            // we found beginn and endline, last thing todo, is to calculate the (maximum) 3 rect wich contains the link:
            style.wordWrap = false;// need to do this, or length of calcsize is wrong.. lol!!!
            int beginlength = (int)chatLogStyle.CalcSize(new GUIContent(linetilllinkstart)).x;
            if (linetilllinkstart == "") { beginlength = 0; }
            int endlength = (int)chatLogStyle.CalcSize(new GUIContent(linetilllinkend)).x;
            style.wordWrap = true;
            Rect[] rects = null;
            //Console.WriteLine("link " + link + " " + beginlength + " " + endlength);
            if (linkbeginhight == linkendhight) // nur ein rect
            {
                rects = new Rect[1];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart + linkbeginhight - normlhight, endlength - beginlength, normlhight);
            }
            if (linkbeginhight + normlhight == linkendhight) // 2 rects
            {
                rects = new Rect[2];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart + linkbeginhight - normlhight, width - beginlength, normlhight);
                rects[1] = new Rect(globalfromxstart, ystart + linkendhight - normlhight, endlength, normlhight);
            }
            if (linkbeginhight + normlhight < linkendhight) // 3 rects
            {
                rects = new Rect[3];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart + linkbeginhight - normlhight, width - beginlength, normlhight);
                rects[1] = new Rect(globalfromxstart, ystart + linkbeginhight, width, linkendhight - normlhight - linkbeginhight);
                rects[2] = new Rect(globalfromxstart, ystart + linkendhight - normlhight, endlength, normlhight);
            }

            //for (int i = 0; i < rects.Length; i++) { Console.WriteLine(rects[i].ToString()); }

            wordclickdata data = new wordclickdata();
            data.wordrects = rects;
            data.lastline = linetilllinkend;
            return data;
        }




        /*private Rect[] linklocator(string link, string mssg, GUIStyle style, float width, int normlhight, int globalfromxstart, int ystart)
        {
            style.wordWrap = true;
            //string txttilllink = Regex.Split(mssg, link.Substring(0,Math.Min(link.Length,10)))[0]; //doesnt work properly, dont want replace a patern, want replace a string!

            string txttilllink = mssg.Split(new string[]{link}, StringSplitOptions.None)[0];
            
            //txttilllink  = Regex.Replace(txttilllink, @"(?></?\w+)(?>(?:[^>'""]+|'[^']*'|""[^""]*"")*)>", String.Empty);
            txttilllink = Regex.Replace(txttilllink, @"(<color=#[A-Za-z0-9]{0,6}>)|(</color>)", String.Empty);
            string txtwithlink = txttilllink + link;
            int linkbeginhight = (int)style.CalcHeight(new GUIContent(txttilllink), width);
            int temp = (int)style.CalcHeight(new GUIContent(txtwithlink), width);
            if (temp > linkbeginhight) { linkbeginhight = linkbeginhight + normlhight; } // if the link is too long, he will start on an extra line
            int ind=0;
            if (!(linkbeginhight == normlhight)) //link hasnt the same hight as normlhight goto first char of line, where the link starts
            {
                
                for (int i = txttilllink.Length; i > 0; i--)
                {
                    if ((txttilllink[i-1] == ' '))
                    {
                        int tmphight = (int)style.CalcHeight(new GUIContent(txttilllink.Substring(0, i)), width);
                        if (tmphight < linkbeginhight) { ind = Math.Min(i + 1,txttilllink.Length); break; };
                    }
                }
               
            }
            string linetilllinkstart = txttilllink.Substring(ind, txttilllink.Length - ind);// we need this for calculating the x-coordinate of link-beginning
            ind  = 0;
            int linkendhight = (int)style.CalcHeight(new GUIContent(txtwithlink), width);
            int linkbeginnindex = txttilllink.Length ;
            string linetilllinkend = linetilllinkstart + link;// we need this for calculating the x-coordinate of link-ending
            if (!(linkbeginhight == linkendhight))      // if the end of the link isnt in the same line like the begining, we have to find the line where it ends 
            {
                for (int i = link.Length; i > 0; i--)
                {
                    int tmphight = (int)style.CalcHeight(new GUIContent(txtwithlink.Substring(0, linkbeginnindex + i)), width);
                    //Console.WriteLine(txtwithlink.Substring(linkbeginnindex, i) + tmphight);
                    if (tmphight < linkendhight) { ind = i+1; break; };
                }
                linetilllinkend = txtwithlink.Substring(linkbeginnindex + ind-1, (link.Length)-ind);
            }
            //Console.WriteLine("Linksearcher:#" + linetilllinkstart + "#and#" + linetilllinkend+"#");

            // we found beginn and endline, last thing todo, is to calculate the (maximum) 3 rect wich contains the link:
            style.wordWrap = false;// need to do this, or length of calcsize is wrong.. lol!!!
            int beginlength = (int)chatLogStyle.CalcSize(new GUIContent(linetilllinkstart)).x;
            if (linetilllinkstart == "") {beginlength=0; }
            int endlength = (int)chatLogStyle.CalcSize(new GUIContent(linetilllinkend)).x;
            style.wordWrap = true;
             Rect[] rects=null;
             //Console.WriteLine("link " + link + " " + beginlength + " " + endlength);
            if (linkbeginhight == linkendhight) // nur ein rect
            {
                rects = new Rect[1];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart+linkbeginhight - normlhight, endlength - beginlength, normlhight);
            }
            if (linkbeginhight + normlhight == linkendhight) // 2 rects
            {
                rects = new Rect[2];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart+linkbeginhight - normlhight, width - beginlength, normlhight);
                rects[1] = new Rect(globalfromxstart, ystart+linkendhight - normlhight, endlength, normlhight);
            }
            if (linkbeginhight + normlhight < linkendhight) // 3 rects
            {
                rects = new Rect[3];
                rects[0] = new Rect(globalfromxstart + beginlength, ystart+linkbeginhight - normlhight, width - beginlength, normlhight);
                rects[1] = new Rect(globalfromxstart, ystart+linkbeginhight, width, linkendhight - normlhight - linkbeginhight);
                rects[2] = new Rect(globalfromxstart, ystart+linkendhight - normlhight, endlength, normlhight);
            }
            
            //for (int i = 0; i < rects.Length; i++) { Console.WriteLine(rects[i].ToString()); }


            return rects;
        }*/


        private void CreateLinkMenu(ChatUser user, string link)
        {
            Vector3 mousePosition = Input.mousePosition;
            // need 30 pixels of extra space per item added

            Rect rect = new Rect(Mathf.Min((float)(Screen.width - 105), mousePosition.x), Mathf.Min((float)(Screen.height - 90 - 5), (float)Screen.height - mousePosition.y), 100f, 30f);


            Gui.ContextMenu<ChatUser>userContextMenu = new Gui.ContextMenu<ChatUser>(user, rect);
            this.globallink = link;
            userContextMenu.add("Open Link", new Gui.ContextMenu<ChatUser>.URCMCallback(OpenLink));

            if (userContextMenu != null)
            {
                userContextMenuField.SetValue(target, userContextMenu);
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
            }

        }



        private void CreateUserMenu(ChatUser user , object infotarget, int length)
	{
        bool canOpenContextMenu = (bool)typeof(ChatUI).GetField("canOpenContextMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(infotarget);
        Gui.ContextMenu<ChatUser> userContextMenu = null;      

		if (!canOpenContextMenu)
		{
			return;
		}
		Vector3 mousePosition = Input.mousePosition;
		Rect rect = new Rect(Mathf.Min((float)(Screen.width - 105), mousePosition.x+100), Mathf.Min((float)(Screen.height - 90 - 5), (float)Screen.height - mousePosition.y), Mathf.Max(100f,length+10), 30f);
		userContextMenu = new Gui.ContextMenu<ChatUser>(user, rect);

        userContextMenu.add(user.name, new Gui.ContextMenu<ChatUser>.URCMCallback(ProfileUser)); 
       
		if (user.acceptTrades)
		{
            userContextMenu.add("Trade", new Gui.ContextMenu<ChatUser>.URCMCallback(TradeUser));
		}

        userContextMenu.add("Whisper", new Gui.ContextMenu<ChatUser>.URCMCallback(whisperclick));

        if (user.acceptChallenges)
        {
            userContextMenu.add("Challenge", new Gui.ContextMenu<ChatUser>.URCMCallback(ChallengeUser));
        }
        userContextMenuField.SetValue(target, userContextMenu);
        App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
        //Console.WriteLine("bums");
	}

        private cardsearcher searchcard(string clickedword)
        {
            cardsearcher result;
            result.clickedcardname = "";
            result.success = false;
            // first: test with underline
            string clink = clickedword;
            if (clink.StartsWith("[") && clink.EndsWith("]"))
            {
                clink = clink.Replace("[", "");
                clink = clink.Replace("]", "");
                clink = clink.Replace("_", " ");
                result.clickedcardname = clink.ToLower();
                result.success = true;
            }
            else
            { 
                // replace [ ] and _
                clink = clink.Replace("[", "");
                clink = clink.Replace("]", "");
                clink = clink.Replace("_", " ");
                clink = clink.ToLower();
                // cardnames with only on word like "burn" are spottet in the first case and dont need to be processed here
                int arrindex = Array.FindIndex(this.cardnames, element => element.Contains(clink));
                
                if (arrindex >= 0)
                {
                    string start = "";
                    string ende = "";
                 // find beginning "["
                    int maxlen = this.longestcardname;
                    if (maxlen > this.clickedwordindex) { maxlen = clickedwordindex; }
                    Console.WriteLine(string.Join(" ", this.clickedtext));
                    Console.WriteLine(maxlen);
                    int i = 0;
                    if (!clickedword.StartsWith("["))
                    {   
                        do{
                            if (maxlen > i)
                            {
                                i++;
                            }
                            else { i = -1; break; }

                        } while ((this.clickedtext[this.clickedwordindex - i].StartsWith("["))==false);
                        if (i == -1) return result;
                        start = string.Join(" ", this.clickedtext, this.clickedwordindex - i, i) + " ";
                    }
                    
                //finding ending "]"
                    maxlen = this.longestcardname;
                    if (maxlen > this.clickedtext.Length-1 - this.clickedwordindex) { maxlen = this.clickedtext.Length-1 - this.clickedwordindex; }
                    i = 0;
                    if (!clickedword.EndsWith("]"))
                    {
                        do
                        {
                            if (maxlen > i)
                            {
                                i++;
                            }
                            else { i = -1; break; }

                        } while ((this.clickedtext[this.clickedwordindex + i].EndsWith("]")) == false);
                        if (i == -1) return result;
                        ende = " " + string.Join(" ", this.clickedtext, this.clickedwordindex + 1, i);
                    }


                    clink = start + clickedword + ende;
                    clink = clink.Replace("[", "");
                    clink = clink.Replace("]", "");
                    clink = clink.Replace("_", " ");
                    result.clickedcardname = clink.ToLower();
                    result.success = true;
                }
            
            
            
            }

           

            return result;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.target is ChatRooms && info.targetMethod.Equals("SetRoomInfo"))
            {
                RoomInfoMessage roomInfo = (RoomInfoMessage)info.arguments[0];
                if (!userNameToUserCache.ContainsKey(roomInfo.roomName))
                {
                    userNameToUserCache.Add(roomInfo.roomName, new Dictionary<string, ChatUser>());
                }
                Dictionary<string, ChatUser> userCache = userNameToUserCache[roomInfo.roomName];
                userCache.Clear();
                RoomInfoProfile[] profiles = roomInfo.updated;
                for (int i = 0; i < profiles.Length; i++)
                {
                    RoomInfoProfile p = profiles[i];
                    ChatUser user = ChatUser.FromRoomInfoProfile(p) ;
                    userCache.Add(user.name, user);
                    if (!globalusers.ContainsKey(user.name)) { globalusers.Add(user.name, user); };
                }

                
                
            }
            /*else if (info.target is ChatUI && info.targetMethod.Equals("Initiate"))
            {
                Console.WriteLine("Initialize#");
                if (target != (ChatUI)info.target)
                {
                    chatRooms = (ChatRooms)chatRoomsinfo.GetValue(info.target);
                    target = (ChatUI)info.target;
                }

            }*/

            else if (info.target is ChatUI && info.targetMethod.Equals("OnGUI"))
            {
                if (target != (ChatUI)info.target)
                {
                    chatRooms = (ChatRooms)chatRoomsinfo.GetValue(info.target);
                    target = (ChatUI)info.target;
                    
                }
                
                RoomLog currentRoomChatLog = chatRooms.GetCurrentRoomChatLog();
                if (currentRoomChatLog != null)
                {

                    if (!(chatRooms.GetCurrentRoom().name == this.currentroom)) { this.currentroom = chatRooms.GetCurrentRoom().name; recalc = true; }

                    Vector2 screenMousePos = GUIUtil.getScreenMousePos();
                    Rect chatlogAreaInner = new Rect((Rect)chatlogAreaInnerinfo.GetValue(info.target));

                    // delete picture on click!
                    if ((Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) && this.clicked == false) { this.clearallpics(); }

                    if (chatlogAreaInner.Contains(screenMousePos) && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) && this.clicked == false)
                    { this.clicked=true;}
                    

                    if (!(Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) && this.clicked)
                    {
                        this.clicked = false;

                        if (!(Screen.height == screenh) || !(Screen.width == screenw)) // if resolution was changed, recalc positions
                        {
                            recalc = true;
                            screenh = Screen.height;
                            screenw = Screen.width;
                        }

                        timeStampStyle = (GUIStyle)timeStampStyleinfo.GetValue(info.target);
                        chatLogStyle = (GUIStyle)chatLogStyleinfo.GetValue(info.target);

                        bool allowSendingChallenges = (bool)allowSendingChallengesinfo.GetValue(info.target);

                        Gui.ContextMenu<ChatUser> userContextMenu = (Gui.ContextMenu<ChatUser>)userContextMenuinfo.GetValue(info.target);
                        float maxScroll = (float)maxScrollinfo.GetValue(info.target);

                        int posy = 0;//

                        float width = (chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f);
                        int globalfromxstart = (int)chatlogAreaInner.xMin + (int)(20f + (float)Screen.height * 0.042f) + 10;
                        int normlhight = (int)chatLogStyle.CalcHeight(new GUIContent("lol"), width);
                        if (recalc) // recalc positions
                        {
                            this.roomuserlist.Clear();
                            //this.roomlinklist.Clear();


                            recalc = false;
                            

                            foreach (RoomLog.ChatLine current in currentRoomChatLog.GetLines())
                            {
                                logentry templog = new logentry();
                                //delete html

                                
                                String shorttxt = Regex.Replace(current.text, @"(<color=#[A-Za-z0-9]{0,6}>)|(</color>)", String.Empty);
                                templog.msg = current.text;

                                
                                templog.from = shorttxt.Split(':')[0];
                                templog.admin = false; //for testing true
                                if (current.senderAdminRole == AdminRole.Mojang)
                                {
                                    templog.admin = true;
                                    //chatLogStyle.fontSize
                                }

                                else
                                {
                                    if (current.senderAdminRole == AdminRole.Moderator)
                                    {
                                        templog.admin = true;
                                    }
                                }

                                chatLogStyle.wordWrap = false;// need to do this, or length of calcsize is wrong.. lol!!!
                                Vector2 v = chatLogStyle.CalcSize(new GUIContent(templog.from + ":"));//calc length of "username:"
                                if (templog.admin)
                                { // "iiii " has the same length like the symbol
                                    v = chatLogStyle.CalcSize(new GUIContent("iiii "+templog.from + ":"));//calc length of "username:"
                                }
                                chatLogStyle.wordWrap = true;

                                
                                float msgheight = chatLogStyle.CalcHeight(new GUIContent(current.text), width);
                                //float msgheight = chatLogStyle.CalcHeight(new GUIContent(shorttxt), width);
                                //Console.WriteLine(current.text + " " + shorttxt + " " + msgheight);
                                int fromxend = (int)v.x + globalfromxstart;
                                templog.usernamelength = (int)v.x;
                                int fromystart = posy;
                                int fromyend = posy + (int)v.y;
                                templog.end = posy + (int)msgheight + 2;//2 is added after each msg
                                posy += (int)msgheight + 2;
                                templog.userrec = new Rect(globalfromxstart, fromystart, fromxend - globalfromxstart, fromyend - fromystart);
                                this.roomuserlist.Add(templog);

                                //Console.WriteLine(current.text+" "+templog.from +" "+shorttxt+" " + v.x + " " + templog.fromxend + " " + msgheight + " " + posy);
                            }



                        }
                        else { posy = this.posycopy; }
                        this.posycopy = posy;
                        Vector2 chatScroll = Vector2.zero + (Vector2)chatScrollinfo.GetValue(info.target);
                        int chatlength = Math.Max(posy + (int)chatlogAreaInner.yMin + 1, (int)chatlogAreaInner.yMax);//take the chatlogareainner.y if msg doesnt need scrollbar (cause its to short)
                        //Maxscroll is FALSE! if you have a small window, with scollbar, and resize it maxscroll wont get lower
                        int truemaxscroll = Math.Max(chatlength - (int)chatlogAreaInner.yMax, 1);//minimum 1 else dividing by zero
                        //calculate upper y-value (=0 if chatscroll.y=0 and =chatlength-yMax if chatscroll.y=truemaxscroll)
                        int currentuppery = (int)((chatlength - (int)chatlogAreaInner.yMax) * (chatScroll.y / truemaxscroll));
                        //calculate mouseposy in chatbox (if maus klicks in the left upper edge of chatlogareainner with scrollbar on the top, the value is zero ) 
                        int realmousy = currentuppery + (int)screenMousePos.y - (int)chatlogAreaInner.yMin;
                        //Console.WriteLine("border chatinner: " + chatlogAreaInner.xMin + " " + chatlogAreaInner.yMin + " " + chatlogAreaInner.xMax + " " + chatlogAreaInner.yMax);
                        //Console.WriteLine("fontsize: " + chatLogStyle.fontSize);
                        //Console.WriteLine("maus " + screenMousePos.x + " " + realmousy);
                        Vector2 mousepos = new Vector2(screenMousePos.x, realmousy);

                        // is an username is clicked?
                        foreach (logentry log in roomuserlist)
                        {
                            Rect usrbutton = log.userrec;
                            if (usrbutton.Contains(mousepos))
                            {
                                //Console.WriteLine("userrecx " + log.userrec.xMin+" "+log.userrec.xMax);
                                //Console.WriteLine("klicked: " + log.from);
                                //##################
                                string sender = log.from;
                                ChatUser user;
                                bool foundUser = globalusers.TryGetValue(sender, out user);
                                if (foundUser)
                                {
                                    user.acceptChallenges = false;
                                    user.acceptTrades = true;
                                    this.CreateUserMenu(user, info.target,log.usernamelength);
                                    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                                }
                                //##################
                                break;
                            }
                            if (mousepos.y >= usrbutton.yMin && mousepos.y <= log.end) 
                            {
                                
                                string klickedword = whitchwordclicked(log.msg, chatLogStyle, width, normlhight, globalfromxstart, (int)usrbutton.yMin+2, mousepos, log.admin);
                                //clickable link?
                                
                                Match match = linkFinder.Match(klickedword);
                                if (match.Success)
                                {
                                    string link = match.Value;
                                    if (!(link.StartsWith(@"https://")) && !(link.StartsWith(@"http://"))) { link = "http://" + link; }
                                    this.CreateLinkMenu(null, link);
                                    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                                }
                                //clickable card?

                                cardsearcher cmatch = searchcard(klickedword);
                                //Match mutch=cardlinkfinder.Match(klickedword);
                                //if (mutch.Success)
                                if (cmatch.success)
                                {
                                    //string clink = mutch.Value.ToLower();
                                    //clink = clink.Replace("[","");
                                    //clink = clink.Replace("]", "");
                                    //clink = clink.Replace("_", " ");
                                    string clink = cmatch.clickedcardname;
                                    Console.WriteLine("CARDCLICKED: " + clink);
                                    int arrindex = Array.FindIndex(this.cardnames, element => element.Equals(clink));
                                    if (arrindex >= 0)
                                    {

                                        CardType type = CardTypeManager.getInstance().get(this.cardids[arrindex]);
                                        Card card = new Card(cardids[arrindex], type, false);
                                        UnityEngine.Object.Destroy(this.cardRule);
                                        cardRule = PrimitiveFactory.createPlane();
                                        cardRule.name = "CardRule";
                                        CardView cardView = cardRule.AddComponent<CardView>();
                                        cardView.init(this,card,0);
                                        cardView.applyHighResTexture();
                                        cardView.setLayer(8);
                                        Vector3 vccopy = Camera.main.transform.localPosition;
                                        Camera.main.transform.localPosition = new Vector3(0f , 1f , -10f);
                                        cardRule.transform.localPosition = Camera.main.ScreenToWorldPoint(new Vector3((float)Screen.width * 0.87f, (float)Screen.height * 0.6f, 0.9f)); ;
                                        //cardRule.transform.localPosition = Camera.main.ScreenToWorldPoint(new Vector3((float)Screen.width * 0.57f, (float)Screen.height * 0.6f, 0.9f)); ;
                                        
                                        cardRule.transform.localEulerAngles = new Vector3(90f, 180f, 0f);//90 180 0
                                        cardRule.transform.localScale = new Vector3(9.3f, 0.1f, 15.7f);// CardView.CardLocalScale(100f);
                                        cardtext=cardRule.renderer.material.mainTexture;
                                        Vector3 ttvec1 = Camera.main.WorldToScreenPoint(cardRule.renderer.bounds.min);
                                        Vector3 ttvec2 = Camera.main.WorldToScreenPoint(cardRule.renderer.bounds.max);
                                        Rect ttrec = new Rect(ttvec1.x, Screen.height - ttvec2.y, ttvec2.x - ttvec1.x, ttvec2.y - ttvec1.y);
                                        
                                        scalefactor =  (float)(Screen.height/1.9)/ttrec.height;
                                        cardRule.transform.localScale = new Vector3(cardRule.transform.localScale.x * scalefactor, cardRule.transform.localScale.y, cardRule.transform.localScale.z * scalefactor);
                                         ttvec1 = Camera.main.WorldToScreenPoint(cardRule.renderer.bounds.min);
                                         ttvec2 = Camera.main.WorldToScreenPoint(cardRule.renderer.bounds.max);
                                         ttrec = new Rect(ttvec1.x, Screen.height - ttvec2.y, ttvec2.x - ttvec1.x, ttvec2.y - ttvec1.y);
                                        cardrect = ttrec;
                                        gettextures(cardView);
                                        mytext = true;
                                        Camera.main.transform.localPosition=vccopy;


                                    }
                                
                                }

                            }
                            if (mousepos.y < log.end) { break; };

                        }//foreach (logentry log in liste) end 

                        // is link klicked
                        /*Rect lastrec = new Rect();
                        foreach (linkdata log in roomlinklist)
                        {
                            Rect[] usrbutton = log.linkrec;
                            bool contain = false;
                            foreach (Rect rec in usrbutton)
                            {
                                
                                contain = contain || rec.Contains(mousepos);
                                lastrec = rec;
                                //Console.WriteLine(contain + " " + rec.ToString());
                            }
                            if (contain)
                            {
                                string link = log.link;

                                //user.acceptChallenges = false;
                                //user.acceptTrades = true;
                                this.CreateLinkMenu(null, link);
                                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");



                                //##################
                                break;
                            }
                            if (mousepos.y < lastrec.yMax) { break; };

                        }*/

                        
                    }

                    // draw cardoverlay again!
                    if (mytext)
                    {
                        GUI.depth =1;
                        Rect rect = new Rect(100,100,100,Screen.height-200);
                        foreach (cardtextures cd in this.gameObjects)
                        {
                            GUI.DrawTexture(cd.cardimgrec, cd.cardimgimg); }

                        foreach (renderwords rw in this.textsArr)
                        {
                            
                            float width =rw.style.CalcSize(new GUIContent(rw.text)).x;
                           GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0),
                            Quaternion.identity, new Vector3(rw.rect.width / width, rw.rect.width / width, 1));
                            
                           Rect lol = new Rect(rw.rect.x * width / rw.rect.width, rw.rect.y * width / rw.rect.width, rw.rect.width * width / rw.rect.width, rw.rect.height * width / rw.rect.width);
                           GUI.contentColor = rw.color;
                           GUI.Label(lol, rw.text, rw.style);
                            
                        }

                    }
                    
                    
                }
            }

            /*
            else if (info.target is ChatRooms && info.targetMethod.Equals("ChatMessage"))
            {
                Console.WriteLine("chatmessage inc##");
                RoomChatMessageMessage msg = (RoomChatMessageMessage)info.arguments[0];
                if (msg.roomName == chatRooms.GetCurrentRoom().name)
                //if (((string)info.arguments[0]) == chatRooms.GetCurrentRoom().name)
                {
                    this.recalc = true;
                }
            }
            */

            else if (info.target is ArenaChat && info.targetMethod.Equals("handleMessage"))
            {
                Message msg = (Message)info.arguments[0];
                if (msg is WhisperMessage)
                {
                    WhisperMessage whisperMessage = (WhisperMessage)msg;
                    if (whisperMessage.GetChatroomName() == chatRooms.GetCurrentRoom().name)
                    {
                        this.recalc = true;
                    }
                }
                if (msg is RoomChatMessageMessage)
                {
                    RoomChatMessageMessage mssg = (RoomChatMessageMessage)msg;
                    if (mssg.roomName == chatRooms.GetCurrentRoom().name)
                    {
                        this.recalc = true;
                    }
                }
            
            }



            return;
        }

        //need following 6 methods for icardrule
        public void HideCardView()
        {
           
        }
        public void SetLoadedImage(Texture2D image, string imageName)
        {
            ResourceManager.instance.assignTexture2D(imageName, image);
            
        }
        public Texture2D GetLoadedImage(string imageName)
        {
            return ResourceManager.instance.tryGetTexture2D(imageName);
        }
        public void ActivateTriggeredAbility(string id, TilePosition pos)
        {
        }
        public void effectAnimDone(EffectPlayer theEffect, bool loop)
        {
            if (loop)
            {
                theEffect.playEffect(0);
            }
            else
            {
                UnityEngine.Object.Destroy(theEffect);
            }
        }
        public void locator(EffectPlayer effect, AnimLocator loc)
        {
        }


        private void gettextures(CardView cardView)
        {
            this.gameObjects.Clear();
            GameObject go1 = (GameObject)cardImageField.GetValue(cardView);
            cardtextures temp1 = new cardtextures();
            temp1.cardimgimg = go1.renderer.material.mainTexture;
            Vector3 vec1 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.min);
            Vector3 vec2 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.max);
            Rect rec = new Rect(vec1.x, Screen.height - vec2.y, vec2.x - vec1.x, vec2.y - vec1.y);
            temp1.cardimgrec = rec;
            if (go1.renderer.enabled) { this.gameObjects.Add(temp1); }

            // card-texture
            temp1 = new cardtextures();
            temp1.cardimgimg = cardtext;
            temp1.cardimgrec = cardrect;
            this.gameObjects.Add(temp1);
            //icon background
            go1 = (GameObject)icoBGField.GetValue(cardView);
            temp1 = new cardtextures();
            temp1.cardimgimg = go1.renderer.material.mainTexture;
            Vector3 ttvec1 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.min);
            Vector3 ttvec2 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.max);
            Rect ttrec = new Rect(ttvec1.x, Screen.height - ttvec2.y, ttvec2.x - ttvec1.x, ttvec2.y - ttvec1.y);
            temp1.cardimgrec = ttrec;
            if (go1.renderer.enabled) { this.gameObjects.Add(temp1); }
            //stats background
            go1 = (GameObject)statsBGField.GetValue(cardView);
            temp1 = new cardtextures();
            temp1.cardimgimg = go1.renderer.material.mainTexture;
            ttvec1 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.min);
            ttvec2 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.max);
            ttrec = new Rect(ttvec1.x, Screen.height - ttvec2.y, ttvec2.x - ttvec1.x, ttvec2.y - ttvec1.y);
            temp1.cardimgrec = ttrec;
            if (go1.renderer.enabled) { this.gameObjects.Add(temp1); }
            //ico
            go1 = (GameObject)icoField.GetValue(cardView);
            temp1 = new cardtextures();
            temp1.cardimgimg = go1.renderer.material.mainTexture;
            ttvec1 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.min);
            ttvec2 = Camera.main.WorldToScreenPoint(go1.renderer.bounds.max);
            ttrec = new Rect(ttvec1.x, Screen.height - ttvec2.y, ttvec2.x - ttvec1.x, ttvec2.y - ttvec1.y);
            temp1.cardimgrec = ttrec;
            if (go1.renderer.enabled) { this.gameObjects.Add(temp1); }

            



            List<GameObject> Images = (List<GameObject>)gosNumHitPointsField.GetValue(cardView);
            foreach (GameObject go in Images)
            {
                cardtextures temp = new cardtextures();
                temp.cardimgimg = go.renderer.material.mainTexture;
                Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                temp.cardimgrec = trec;
                if (go.renderer.enabled){ this.gameObjects.Add(temp);}
            }

            //ability background
            Images = (List<GameObject>)gosactiveAbilityField.GetValue(cardView);
            foreach (GameObject go in Images)
            {
                if (go.name == "Trigger_Ability_Button")
                {
                    cardtextures temp = new cardtextures();
                    temp.cardimgimg = go.renderer.material.mainTexture;
                    Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                    Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                    Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                    temp.cardimgrec = trec;
                    if (go.renderer.enabled) { this.gameObjects.Add(temp); }
                    break;
                }
            }

            Images = (List<GameObject>)gosNumCostField.GetValue(cardView);
            foreach (GameObject go in Images)
            {
                cardtextures temp = new cardtextures();
                temp.cardimgimg = go.renderer.material.mainTexture;
                Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                temp.cardimgrec = trec;
                if (go.renderer.enabled) { this.gameObjects.Add(temp); }
            }
            Images = (List<GameObject>)gosNumAttackPowerField.GetValue(cardView);
            foreach (GameObject go in Images)
            {
                cardtextures temp = new cardtextures();
                temp.cardimgimg = go.renderer.material.mainTexture;
                Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                temp.cardimgrec = trec;
                if (go.renderer.enabled) { this.gameObjects.Add(temp); }
            }
            Images = (List<GameObject>)gosNumCountdownField.GetValue(cardView);
            foreach (GameObject go in Images)
            {
                cardtextures temp = new cardtextures();
                temp.cardimgimg = go.renderer.material.mainTexture;
                Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                temp.cardimgrec = trec;
                if (go.renderer.enabled) { this.gameObjects.Add(temp); }
            }

            textsArr.Clear();
            Images = (List<GameObject>)textsArrField.GetValue(cardView);
  
            foreach (GameObject go in Images)
            {
                TextMesh lol = go.GetComponentInChildren<TextMesh>();
                renderwords stuff;
                stuff.text = lol.text;
                Vector3 tvec1 = Camera.main.WorldToScreenPoint(go.renderer.bounds.min);
                Vector3 tvec2 = Camera.main.WorldToScreenPoint(go.renderer.bounds.max);
                Rect trec = new Rect(tvec1.x, Screen.height - tvec2.y, tvec2.x - tvec1.x, tvec2.y - tvec1.y);
                stuff.rect = trec;
                GUIStyle style = new GUIStyle();
                style.font = lol.font;
                style.alignment = (TextAnchor)lol.alignment;
                style.fontSize = (int)(lol.fontSize);
                style.wordWrap = false;
                style.stretchHeight = false;
                style.stretchWidth = false;
                stuff.color = new Color(go.renderer.material.color.r, go.renderer.material.color.g, go.renderer.material.color.b, 0.9f);
                style.normal.textColor = stuff.color;
                stuff.style = style;
                textsArr.Add(stuff);

            }

        }


        private void clearallpics()
        {
            UnityEngine.Object.Destroy(this.cardRule);
        textsArr = new List<renderwords>();
        gameObjects = new List<cardtextures>();
        this.mytext = false;
        }
    }
}