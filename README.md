# API请求集中代理网关
可将对第三方API发起的请求集中到一台服务器发起，便于突破IP白名单限制。
### 部署脚本
```
docker run -d -p 80:80 -p 443:443 --restart=always --name onegate ccr.ccs.tencentyun.com/wlniao/onegate
```