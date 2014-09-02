namespace RohBot.Commands
{
    public class Repo : Command
    {
        public override string Type { get { return "wiki"; } }

        public override string Format(CommandTarget target, string type) { return "]"; }

        public override void Handle(CommandTarget target, string type, string[] parameters)
        {
            if ( !target.IsRoom )
                return;

            var room = target.Room;
            if (target.IsWeb)
            {
                var username = target.Connection.Session.Account.Name;
                if (room.IsBanned(username))
                {
                    target.Send("You are banned from this room.");
                    return;
                }
            }
            var send;
            if (parameters.Length == 0) {
                send = "https://github.com/gmodcoders/";
            } else {
                var exp = parameters[0].Split(' ');
                if (exp[0] != null) {
                    string repo = exp[0];
                    string rest = string.Join('/',exp.Skip(1));
                    send = "https://github.com/gmodcoders/"+repo+"/tree/master/"+rest;
                }
            }
            target.Room.SendLine(send);

            return;
        }
    }
}
