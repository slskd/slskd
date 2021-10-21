const rootUrl = process.env.NODE_ENV === 'production' ? window.location.href : 'http://localhost:5000/';
const baseUrl = `${rootUrl}api/v0`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';

export {
    rootUrl,
    baseUrl,
    tokenKey,
    tokenPassthroughValue,
    activeChatKey,
    activeRoomKey,
    activeUserInfoKey
};
