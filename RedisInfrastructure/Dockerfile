FROM redis:7-alpine

RUN mkdir -p /usr/local/etc/redis

RUN echo "bind 0.0.0.0" > /usr/local/etc/redis/redis.conf && \
    echo "protected-mode no" >> /usr/local/etc/redis/redis.conf && \
    echo "port 6379" >> /usr/local/etc/redis/redis.conf && \
    echo "appendonly yes" >> /usr/local/etc/redis/redis.conf && \
    echo "appendfilename appendonly.aof" >> /usr/local/etc/redis/redis.conf && \
    echo "save 900 1" >> /usr/local/etc/redis/redis.conf && \
    echo "save 300 10" >> /usr/local/etc/redis/redis.conf && \
    echo "loglevel notice" >> /usr/local/etc/redis/redis.conf && \
    echo "maxmemory 256mb" >> /usr/local/etc/redis/redis.conf && \
    echo "maxmemory-policy allkeys-lru" >> /usr/local/etc/redis/redis.conf

EXPOSE 6379

CMD ["redis-server", "/usr/local/etc/redis/redis.conf"]