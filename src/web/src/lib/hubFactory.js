import {
  JsonHubProtocol,
  HubConnectionBuilder,
  LogLevel
} from '@microsoft/signalr'

import { rootUrl } from '../config';
import * as session from '../lib/session';

export const createHubConnection = ({ url }) => 
  new HubConnectionBuilder()
    .withUrl(url, {
        accessTokenFactory: session.getToken
    })
    .withAutomaticReconnect()
    .withHubProtocol(new JsonHubProtocol())
    .configureLogging(LogLevel.Information)
    .build();

export const createApplicationHubConnection = () => createHubConnection({ url: `${rootUrl}/hub/application` });