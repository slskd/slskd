const baseUrl = process.env.NODE_ENV === 'production' ? 'api/v1' : 'http://localhost:5000/api/v1';
const tokenKey = 'soulseek-example-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'soulseek-example-active-chat';
const activeRoomKey = 'soulseek-example-active-room';

export {
    baseUrl,
    tokenKey,
    tokenPassthroughValue,
    activeChatKey,
    activeRoomKey
};
