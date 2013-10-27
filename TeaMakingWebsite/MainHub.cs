using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using Microsoft.AspNet.SignalR;

namespace TeaMakingWebsite
{
    public class MainHub : Hub
    {
        private const decimal MIN_FRACTION_READY = 0.5M;
        static readonly List<UserInfo> _users = new List<UserInfo>(); 
        private class UserInfo
        {
            public string ConnectionID { get; set; }
            public string Username { get; set; }
            public bool Ready { get; set; }
        }

        private static State CurrentState = State.Waiting;
        static UserInfo _maker=null;

        public override System.Threading.Tasks.Task OnConnected()
        {
            if (_users.All(u => u.ConnectionID != Context.ConnectionId))
            {
                var userInfo = new UserInfo
                                   {
                                       ConnectionID=Context.ConnectionId,
                                       Username = Context.QueryString["Username"],
                                       Ready = false
                                   };
                                _users.ToList().ForEach(u => Clients.Client(Context.ConnectionId).AddUser(
                                    u.Username,
                                    u.Ready));
                                _users.Add(userInfo);
                Clients.AllExcept(Context.ConnectionId).AddUser(userInfo.Username,userInfo.Ready);
            }
            return base.OnConnected();
        }
        public override System.Threading.Tasks.Task OnDisconnected()
        {
            var toRemove = _users.FirstOrDefault(u => u.ConnectionID == Context.ConnectionId);
            if (toRemove!=null)
            {
                _users.Remove(toRemove);
                Clients.AllExcept(Context.ConnectionId).RemoveUser(toRemove.Username);
                if (_maker != null && toRemove.ConnectionID == _maker.ConnectionID)
                {
                    AcceptOrDeclineMaking(false);
                }
            }
            return base.OnDisconnected();
        }
        public void ToggleReady()
        {
            var user = _users.FirstOrDefault(u => u.ConnectionID == Context.ConnectionId);
            user.Ready = !user.Ready;
            Clients.All.SetUserReady(user.Username, user.Ready);
            if (user.Ready && CurrentState==State.Waiting && (decimal) _users.Count(u => u.Ready)/_users.Count() >= MIN_FRACTION_READY)
            {
                CurrentState = State.Asking;
                AskToMakeTea();
            }
        }

        private void AskToMakeTea()
        {
            _maker = _users.Where(u => u.Ready).OrderBy(u => Guid.NewGuid()).FirstOrDefault();
            if(_maker != null)
                Clients.Client(_maker.ConnectionID).MakeTea();
            else
                CurrentState = State.Waiting;
        }

        public void AcceptOrDeclineMaking(bool answer)
        {
            if(answer)
            {
                Clients.AllExcept(Context.ConnectionId).TeaBeingMade(_users.FirstOrDefault(u=>u.ConnectionID==Context.ConnectionId).Username);
                _users.ForEach(user =>
                                   {
                                       user.Ready = false;
                                       Clients.All.SetUserReady(user.Username, user.Ready);
                                   });
                CurrentState = State.Waiting;
                _maker = null;
            }
            else
            {
                AskToMakeTea();
            }
        }
        private enum State
        {
            Waiting,
            Asking
        }
    }

}