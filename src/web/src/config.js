const baseUrl = process.env.NODE_ENV === 'production' ? 'api/v0' : 'http://localhost:5000/api/v0';
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'sslskd-active-room';

export {
    baseUrl,
    tokenKey,
    tokenPassthroughValue,
    activeChatKey,
    activeRoomKey
};
