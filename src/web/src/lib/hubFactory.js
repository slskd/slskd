import {
  JsonHubProtocol,
  HubConnectionBuilder,
  LogLevel
} from '@microsoft/signalr'

import { rootUrl } from '../config';
import * as session from '../lib/session';

export const createApplicationHubConnection = () =>
  new HubConnectionBuilder()
    .withUrl(`${rootUrl}/hub/application`, {
        accessTokenFactory: session.getToken
    })
    .withAutomaticReconnect()
    .withHubProtocol(new JsonHubProtocol())
    .configureLogging(LogLevel.Information)
    .build();