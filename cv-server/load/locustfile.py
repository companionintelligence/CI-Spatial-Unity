import os

from locust import HttpUser, task, between


class CvServerUser(HttpUser):
    wait_time = between(0.2, 0.8)

    def on_start(self):
        # The server requires a bearer token on every endpoint.
        token = os.environ.get("CV_SERVER_API_TOKEN", "").split(",")[0].strip()
        if not token:
            raise RuntimeError("Set CV_SERVER_API_TOKEN to the server's API token.")
        self.client.headers["Authorization"] = f"Bearer {token}"

    @task(3)
    def detect(self):
        self.client.post(
            "/v1/detect",
            json={
                "image": self.environment.parsed_options.image_b64,
                "prompt": "chair . table .",
                "box_threshold": 0.35,
                "text_threshold": 0.25,
                "max_side": 640,
            },
        )

    @task(1)
    def segment(self):
        self.client.post(
            "/v1/segment",
            json={
                "image": self.environment.parsed_options.image_b64,
                "prompt": "chair . table .",
                "box_threshold": 0.35,
                "text_threshold": 0.25,
                "grounding_model": "grounding_dino",
                "return_masks": False,
                "mask_format": "rle",
                "max_side": 640,
            },
        )


def add_arguments(parser):
    parser.add_argument(
        "--image-b64",
        dest="image_b64",
        required=True,
        help="Base64-encoded JPEG payload for load tests",
    )
