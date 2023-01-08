# Running Behind a Reverse Proxy

## NGINX

Because slskd uses websockets in addition to standard HTTP traffic, a couple of headers need to be defined along with the standard `proxy_pass` to enable everything to work properly.

Assuming a default NGINX installation, create `/etc/nginx/conf.d/slskd.conf` and populate it with one of the configurations below, depending on whether you'd like to serve slskd from a subdirectory or the root of the webserver.

### At a Subdirectory

In this scenario `URL_BASE` must be set to `/slskd`

```
server {
        listen 8080;

        location /slskd {
                proxy_pass http://localhost:5000;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "Upgrade";
                proxy_set_header Host $host;
        }
}
```

### At the Root

In this scenario `URL_BASE` is left to the default value of `/`

```
server {
        listen 8080;

        location / {
                proxy_pass http://localhost:5000;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "Upgrade";
                proxy_set_header Host $host;
        }
}
```