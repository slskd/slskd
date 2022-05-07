const urlBase = (window.urlBase === '/' ? '' : window.urlBase) || '';
const rootUrl = process.env.NODE_ENV === 'production' ? urlBase : `http://localhost:5000/${urlBase}`;
const apiBaseUrl = `${rootUrl}/api/v0`;
const hubBaseUrl = `${rootUrl}/hub`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';

export {
    urlBase,
    rootUrl,
    apiBaseUrl,
    hubBaseUrl,
    tokenKey,
    tokenPassthroughValue,
    activeChatKey,
    activeRoomKey,
    activeUserInfoKey
};
