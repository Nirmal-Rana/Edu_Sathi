//room-live.js
/*
    EduSathi - realtime room updates (Questionnaires "Global" flow).

    Talks to Hubs/RoomHub.cs over /hubs/room. Only runs on pages that opted in
    by including this script (RoomLobby.cshtml, and RoomQuiz.cshtml for Global
    rooms) - both only pull it in for Global rooms, since Solo has no group to
    join and nobody else to hear from.

    Markup contract:
      <div data-room-code="ABC123">           the code this page belongs to
      <div id="participantsList">              lobby only - rows get appended here
      <span id="participantCount">             lobby only - "(N)" text updated in place
      <div id="leaderboardList" data-leaderboard>  results page only - rebuilt on update
*/
(function () {
    "use strict";

    function escapeHtml(value) {
        var div = document.createElement("div");
        div.textContent = value == null ? "" : String(value);
        return div.innerHTML;
    }

    function init() {
        var host = document.querySelector("[data-room-code]");
        if (!host || typeof signalR === "undefined") return;

        var roomCode = host.getAttribute("data-room-code");
        if (!roomCode) return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/room")
            .withAutomaticReconnect()
            .build();

        var participantsList = document.getElementById("participantsList");
        var participantCount = document.getElementById("participantCount");
        var leaderboardList = document.getElementById("leaderboardList");

        if (participantsList) {
            connection.on("ParticipantJoined", function (data) {
                var row = document.createElement("div");
                row.className = "list-item";
                row.style.marginBottom = "10px";
                row.innerHTML =
                    '<div class="icon-circle purple"><i class="bi bi-person-fill"></i></div>' +
                    '<div class="info"><div class="title">' + escapeHtml(data.displayName) + '</div></div>' +
                    '<span class="tag-current" style="background:var(--green-soft-bg);color:var(--green);">JOINED</span>';
                participantsList.appendChild(row);

                if (participantCount) {
                    var current = participantsList.querySelectorAll(".list-item").length;
                    participantCount.textContent = "(" + current + ")";
                }
            });
        }

        connection.on("RoomStarted", function (data) {
            if (data && data.redirectUrl) {
                window.location.href = data.redirectUrl;
            }
        });

        if (leaderboardList) {
            connection.on("LeaderboardUpdated", function (data) {
                var entries = (data && data.entries) || [];
                leaderboardList.innerHTML = "";
                entries.forEach(function (entry, index) {
                    var rank = index + 1;
                    var row = document.createElement("div");
                    row.className = "list-item";
                    row.style.marginBottom = "10px";

                    var iconClass = rank === 1 ? "orange" : "purple";
                    var iconInner = rank === 1
                        ? '<i class="bi bi-trophy-fill"></i>'
                        : "<span>" + rank + "</span>";
                    var metaText = entry.score != null
                        ? (entry.score + "/" + entry.total + " correct")
                        : "Still playing\u2026";

                    row.innerHTML =
                        '<div class="icon-circle ' + iconClass + '">' + iconInner + '</div>' +
                        '<div class="info"><div class="title">' + escapeHtml(entry.displayName) +
                        (entry.isHost ? " (Host)" : "") + '</div>' +
                        '<div class="meta">' + metaText + '</div></div>';
                    leaderboardList.appendChild(row);
                });
            });
        }

        connection.start()
            .then(function () { return connection.invoke("JoinRoomGroup", roomCode); })
            .catch(function (err) { console.error("Room hub connection failed:", err); });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();