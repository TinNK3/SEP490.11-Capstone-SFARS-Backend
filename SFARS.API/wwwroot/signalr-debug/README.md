Open this page from the backend host so it shares the same origin as the API:

- `https://<your-backend-host>/signalr-debug/index.html`

Suggested test flow for mission status:

1. Paste a valid JWT token for the victim or rescuer.
2. Click `Connect Hub`.
3. Enter `incidentId` and click `JoinIncidentTracking`.
4. Enter `missionId`.
5. Click `PATCH /api/missions/{id}/status`.
6. Check the log for:
   - `PATCH mission status response: 200`
   - `SignalR event: sos:mission_status`

Suggested test flow for video call:

1. Open the page in two browser windows with two different tokens.
2. In both windows click `Connect Hub`.
3. In both windows use the same `incidentId`.
4. In at least the participant that should receive room events, click `JoinIncidentTracking`.
5. In one window click `POST /api/video-call/initiate/{incidentId}`.
6. Check the other window for `SignalR event: CallIncoming`.
7. Use `Invoke AcceptVideoCall`, `Invoke RejectVideoCall`, or `Invoke EndVideoCall` to test the rest of the lifecycle.

Notes:

- `CallIncoming` is user-targeted on `LocationTrackingHub`.
- `sos:mission_status`, `CallAccepted`, `CallRejected`, and `CallEnded` are incident-room events.
- If you do not see room events, verify that `JoinIncidentTracking` succeeded for the same `incidentId`.
