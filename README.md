# API请求集中代理网关
可将对第三方API发起的请求集中到一台服务器发起，便于突破IP白名单限制。
### 部署脚本
```
docker run -d -p 80:80 -p 443:443 --restart=always \
-e HTTPS=true \
-e DOMAIN_wxapi=api.weixin.qq.com \
-e DOMAIN_ddapi=oapi.dingtalk.com \
--name onegate wlniao/onegate
```
### 变量说明
* `HTTPS=true`	通过HTTPS发起请求
* `DOMAIN=`	默认需要转发的真实地址
* `DOMAIN_wxapi=`	域名`wxapi`.yourdomain.com访问时转发的地址
* `DOMAIN_ddapi=`	域名`ddapi`.yourdomain.com访问时转发的地址
