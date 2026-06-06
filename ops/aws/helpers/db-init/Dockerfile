FROM cgr.dev/chainguard/python:latest-dev AS build
RUN pip install --no-cache-dir psycopg2-binary

FROM cgr.dev/chainguard/python:latest
COPY --from=build /home/nonroot/.local /home/nonroot/.local
COPY init.py /home/nonroot/init.py
ENTRYPOINT ["python", "/home/nonroot/init.py"]
