
var storage;
try {
	var uid = new Date;
	(storage = window.localStorage).setItem(uid, uid);
	var fail = storage.getItem(uid) != uid;
	storage.removeItem(uid);
	fail && (storage = false);
} catch(e) { storage = false; }

var roomName;
var requestedHistory;
var oldestMessage;

var rohbot = null;
function initializeRohBot() {
	var serverUri = "wss://fpp.literallybrian.com/ws/";
	if (window.location.search == "?noproxy")
		serverUri = "ws://fpp.literallybrian.com:12000/";
	
	rohbot = new RohBot(serverUri); 
	rohbot.onConnected = function() {
		oldestMessage = 0xFFFFFFFF;
		requestedHistory = false;
		
		var room = window.location.hash.substring(1);
		if (storage && storage.getItem("password") !== null) {
			rohbot.login(storage.getItem("name"), storage.getItem("password"), null, room);
		} else if (storage && storage.getItem("tokens") !== null) {
			rohbot.login(storage.getItem("name"), null, storage.getItem("tokens"), room);
		} else {
			rohbot.login("guest", null, null, room);
		}
	};
	
	rohbot.onLogin = function(data) {
		if (storage) {
			storage.setItem("name", data.Name);
			storage.setItem("tokens", data.Tokens);
		}
			
		if (data.Success) {
			$("#header").hide();
			$("#messageBox").removeAttr("disabled");
			$("#messageBox").val("");
			$("#password").val("");
		} else {
			$("#header").show();
			$("#messageBox").attr("disabled","disabled");
			$("#messageBox").val("Guests can not speak.");
		}
		
		$("#chat").scrollTop($("#chat")[0].scrollHeight);
	};
	
	rohbot.onChatHistory = function(data) {
		if (!data.Requested) {
			$("#chat").html("");
			for (var i = 0; i < data.Lines.length; i++) {
				addRohBotMessage(data.Lines[i], data.Requested);
			}
			$("#chat").scrollTop($("#chat")[0].scrollHeight);
		} else {
			var firstMsg = $("#chat :first");
			
			for (var i = data.Lines.length - 1; i >= 0; i--) {
				addRohBotMessage(data.Lines[i], data.Requested);
			}
			
			requestedHistory = false;
			
			var header = $("#header");
			var headerHeight = header.is(":visible") ? header.height() + 10 : 0;
			$("#chat").scrollTop(firstMsg.offset().top - headerHeight - 20);
		}
		
		roomName = data.Name;
		document.title = roomName;
		$("#title").text(roomName);
		oldestMessage = data.OldestLine;
	};
	
	rohbot.onMessage = function(line) {
		if (storage && window.webkitNotifications &&
			window.webkitNotifications.checkPermission() === 0 &&
			line.Type == "chat" && line.Sender != rohbot.name)
		{
			var regexStr = storage.getItem("notify");
			if (regexStr !== null && regexStr.length > 0) {
				try {
					var regex = new RegExp(regexStr, "gim");
					if (regex.test(line.Content)) {
						var notification = window.webkitNotifications.createNotification(
							'rohbot.png',
							roomName,
							htmlDecode(line.Sender) + ": " + htmlDecode(line.Content)
						);
						
						setTimeout(function() {
							notification.close();
						}, 3000);
						
						notification.onclick = function() {
							notification.close();
						}
						
						notification.show();
					}
				} catch (e) {
					console.log(e.message);
				}
			}
		}
		
		addRohBotMessage(line, false);
	};
	
	rohbot.onSysMessage = function(line) {
		line.Type = "state";
		addRohBotMessage(line, false);
	};
	
	rohbot.onUserList = function(users) {
		addRohBotMessage({ Type: "state", Date: getCurrentTime(), Content: "In this room:" });
		var html = '<div class="userList">';
		for (var i = 0; i < users.length; i++) {
			var user = users[i];
			
			if (user.Name == "Guest")
				continue;
				
			if (user.Avatar == "0000000000000000000000000000000000000000")
				user.Avatar = "fef49e7fa7e1997310d705b2a6158ff8dc1cdfeb";
			
			html += '<div>';
			var inGame = user.Playing != "";
			var game = user.Playing != "" ? "In-Game: " + user.Playing : "&nbsp;";
			var avatarUrl = "/steamcommunity/public/images/avatars/" + user.Avatar.substring(0, 2) + "/" + user.Avatar + "_full.jpg";
			if (user.Avatar == "") avatarUrl = "rohbot.png";
			if (!user.Web) user.Name = '<a href="http://steamcommunity.com/profiles/' + user.UserId + '" target="_blank">' + user.Name + '</a>';
			html += '<img src="' + avatarUrl + '"/>';
			html += '<div data-color="' + (inGame ? "ingame" : (user.Web ? "web" : "")) + '" data-rank="' + user.Rank + '">' + user.Name + '<br/>' + game + '</div>';
			html += '</div>';
		}
		
		html += '</div/>';
		addHtml(html, false);
	};
	
	rohbot.connect();
}

function addRohBotMessage(line, oldLine) {
	switch (line.Type) {
		case "chat": {
			var sender = line.Sender;
			if (line.UserType == "RohBot")
				sender = '<span class="rohBot ' + line.SenderStyle + '">' + sender + '</span>';
			else if (line.InGame)
				sender = '<span class="inGame">' + sender + '</span>';
			
			var html = '<div><span class="sender">' + formatTime(line.Date) + ' - ' + sender + ': </span>' + linkify(line.Content) + '</div>';
			addHtml(html, oldLine);
			break;
		}
		
		case "state": {
			var html = '<div><span class="sender">' + formatTime(line.Date) + ' - </span>' + line.Content + '</div>';
			addHtml(html, oldLine);
			break;
		}
		
		case "whisper": {
			line.Type = "chat";
			if (line.Sender === rohbot.name)
				line.Sender = "To " + line.Receiver;
			else
				line.Sender = "From " + line.Sender;
			addRohBotMessage(line, oldLine);
			break;
		}
	}
};

function addHtml(html, old) {
	var elem = $("#chat");
	var atBottom = (elem.outerHeight() >= (elem[0].scrollHeight - elem.scrollTop() - 32));
	
	if (!old)
		$("#chat").append(html);
	else
		$("#chat").prepend(html);
		
	if (!old && atBottom)
		elem.scrollTop(elem[0].scrollHeight);
}

function formatTime(date) {
	date = new Date(date * 1000);
	var suffix = "";
	var hour = date.getHours();
	
	if (hour < 12)
		suffix = "AM";
	else
		suffix = "PM";
	
	if (hour == 0)
		hour = 12;
	if (hour > 12)
		hour = hour - 12;
		
	var min = date.getMinutes();
	if (min.toString().length == 1)
		min = "0" + min;
		
	return hour + ":" + min + " " + suffix;
}

function linkify(str) {
	str = urlize(str, { target: '_blank' }).replace(/\n/g, '<br/>');
	var res = $("<div/>");
	var e = $("<div/>");
	e.html(str).contents().each(function (i, elem) {
		if (elem.nodeType == 3) {
			res.append(htmlEncode(elem.textContent).replace(/ː(.+?)ː/img, '<img src="/economy/emoticon/$1"/>'));
		} else {
			res.append(elem);
		}
	});
	return res.html();
}

function htmlEncode(html) {
	return document.createElement('a').appendChild(document.createTextNode(html)).parentNode.innerHTML;
}

function htmlDecode(html) {
	var a = document.createElement('a');
	a.innerHTML = html;
	return a.textContent;
}

function getCurrentTime() {
	return new Date().getTime() / 1000;
}

$(document).ready(function() {
	initializeRohBot();

	window.chat = new ChatManager( rohbot );
	
	$("#password").keydown(function(e) {
		if (e.keyCode == 13) {
			$("#password").blur().focus();
			$("#loginButton").click();
			return false;
		}
	});
	
	$("#loginButton").click(function() {
		rohbot.login($("#username").val(), $("#password").val(), null);
	});
	
	$("#registerButton").click(function() {
		rohbot.register($("#username").val(), $("#password").val());
	});
	
	$("#chat").scroll(function() {
		if ($("#chat").scrollTop() == 0 && !requestedHistory) {
			rohbot.requestHistory(oldestMessage);
			requestedHistory = true;
		}
	});
	
	$(window).resize(function() {
		$("#chat").scrollTop($("#chat")[0].scrollHeight);
	});
});
