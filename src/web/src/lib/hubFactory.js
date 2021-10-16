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
    .withAutomaticReconnect([0, 500, 1000, 3000, 5000, 5000, 5000, 5000, 5000])
    .withHubProtocol(new JsonHubProtocol())
    .configureLogging(LogLevel.Warning)
    .build();

export const createApplicationHubConnection = () => createHubConnection({ url: `${rootUrl}/hub/application` });

export const createLogsHubConnection = () => createHubConnection({ url: `${rootUrl}/hub/logs` });