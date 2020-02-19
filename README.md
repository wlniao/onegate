# API请求集中代理网关
可将对第三方API发起的请求集中到一台服务器发起，便于突破IP白名单限制。
### 部署脚本
```
docker run -d -p 80:80 -p 443:443 --restart=always \
-e HTTPS=true \
-e DOMAIN=oapi.dingtalk.com \
--name onegate wlniao/onegate
```
### 变量说明
* `HTTPS=true`	通过HTTPS发起请求
* `DOMAIN=oapi.dingtalk.com`	需要请求的真实地址