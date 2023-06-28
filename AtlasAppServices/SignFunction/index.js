import { S3Client, PutObjectCommand, DeleteObjectCommand, GetObjectCommand, HeadObjectCommand, NotFound, S3 } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";

const publicFiles = true;
const validity = 3600;

async function main(payload) {
    const bucket = context.values.get("S3Bucket");
    const accessKeyId = context.values.get("S3AccessKeyIdValue");
    const secretAccessKey = context.values.get("S3SecretAccessKeyValue");
    const region = context.values.get("S3Region");
    const userId = context.user.id;

    const client = new S3Client({
        region,
        credentials: {
            accessKeyId,
            secretAccessKey
        }
    });

    const getCanonicalUrl = (id) => {
        return `https://${bucket}.s3.${region}.amazonaws.com/${id}`;
    }

    const getMetadata = async (id) => {
        try {
            const headCommand = new HeadObjectCommand({ Bucket: bucket, Key: id });
            const response = await client.send(headCommand);
            return response.Metadata || {};
        } catch (err) {
            // There's a bug with the S3 SDK on Atlas Functions - the type hierarchy is messed up and
            // it doesn't have a name property and instanceof NotFound returns false.
            if (`${err}`.indexOf("NotFound") !== -1) {
                return undefined;
            }

            console.log(JSON.stringify(err));
            throw err;
        }
    }

    try {
        switch (payload.Operation) {
            case "Upload":
                const metadata = await getMetadata(payload.FileId);
                if (metadata !== undefined) {
                    return {
                        Success: false,
                        Error: "Object already exists"
                    };
                }

                const uploadCommand = new PutObjectCommand({
                    Bucket: bucket,
                    Key: payload.FileId,
                    ACL: publicFiles ? "public-read" : "private",
                    Metadata: {
                        userid: userId,
                    },
                });

                const uploadUrl = await getSignedUrl(client, uploadCommand, {
                    expiresIn: validity,
                });

                return {
                    Success: true,
                    PresignedUrl: uploadUrl,
                    CanonicalUrl: getCanonicalUrl(payload.FileId),
                };

            case "Download":
                // If files are publicly accessible, there's no need to generate a signed url
                // for the download path.
                if (publicFiles) {
                    return {
                        Success: true,
                        Url: getCanonicalUrl(payload.FileId),
                    };
                }

                const downloadCommand = new GetObjectCommand({ Bucket: bucket, Key: payload.FileId });
                const downloadUrl = await getSignedUrl(client, downloadCommand, { expiresIn: validity });

                return {
                    Success: true,
                    Url: downloadUrl
                };

            case "Delete":
                const deleteMetadata = await getMetadata(payload.FileId);
                if (deleteMetadata === undefined) {
                    return {
                        Success: false,
                        Error: "Object not found"
                    };
                } else if (deleteMetadata.userid !== userId) {
                    return {
                        Success: false,
                        Error: "User issuing the delete needs to match the user that created the request"
                    };
                }

                const deleteCommand = new DeleteObjectCommand({ Bucket: bucket, Key: payload.FileId });
                const response = await client.send(deleteCommand);
                return {
                    Success: response.$metadata.httpStatusCode === 204
                }
            default:
                return {
                    Success: false,
                    Error: `Unknown operation: ${payload.Operation}`
                };
        }
    } catch (err) {
        console.log(`An error occurred executing the function: ${err}`);

        return {
            Success: false,
            Error: "Internal Error. See logs for more details"
        };
    }
}

exports = main;
