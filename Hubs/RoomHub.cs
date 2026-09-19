using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace EduSathi.Hubs
{
    // Lightweight realtime channel for the Questionnaires "Global" room flow.
    // Groups are keyed by the room's shareable code, so everyone on the lobby or
    // quiz page for a given room receives the same events:
    //
    //   ParticipantJoined { displayName }        - someone joined the lobby
    //   RoomStarted       { redirectUrl }         - host pressed "Start Live Quiz"
    //   LeaderboardUpdated { entries: [...] }     - someone finished the quiz
    //
    // The hub itself does no authorization - the room code is the only thing a
    // client needs to join a group with. That's fine: nothing here changes room
    // state. All of that still happens in QuestionnairesController, which is
    // [Authorize] and checks the QuizRoomParticipant rows in the database before
    // doing anything meaningful. This hub only fans the resulting events back out.
    public class RoomHub : Hub
    {
        public async Task JoinRoomGroup(string roomCode)
        {
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, roomCode.Trim().ToUpperInvariant());
            }
        }
    }
}