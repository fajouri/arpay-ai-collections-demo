FROM python:3.12-slim
WORKDIR /app
COPY src/McpServer/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY src/McpServer/ .
EXPOSE 8080
CMD ["python","server.py"]
