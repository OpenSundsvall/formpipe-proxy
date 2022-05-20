using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

using Newtonsoft.Json;
using NSwag.Annotations;

using FormpipeProxy.Models;
using FormpipeProxy.Integration;

using FormPipe.LTA.Contracts.ServiceContracts;

namespace FormpipeProxy.Controllers
{
    public class FormpipeProxyController : ApiController
    {
        private readonly IFormpipeClient client = new FormpipeClient();

        private static readonly SHA256CryptoServiceProvider encrypter = new SHA256CryptoServiceProvider();

        LogFunctions log = new LogFunctions();

        private static Func<ErrorInfo, ErrorDetails> toErrorDetails = errorInfo => new ErrorDetails()
        {
            ErrorId = errorInfo.ErrorId,
            ErrorCode = errorInfo.ErrorCode,
            ErrorMessage = errorInfo.ErrorMessage
        };

        // TODO: search endpoints ???

        [HttpPost]
        [Route("api/import")]
        [OpenApiOperation("import")]
        [SwaggerResponse(HttpStatusCode.OK, typeof(ImportResponse), Description = "Successful operaiton")]
        [SwaggerResponse(HttpStatusCode.InternalServerError, typeof(ImportResponse), Description = "Import failure")]
        public HttpResponseMessage Import([FromBody][Required] ImportRequest request)
        {           
            var errorID = Guid.NewGuid().ToString();
            var returnSetId = "";
            try
            {
                // Import preservation object
                var importPreservationObjectResponse = ImportPreservationObject(request);
                if (importPreservationObjectResponse.ErrorInfo != null && importPreservationObjectResponse.ErrorInfo.ErrorCode != 0)
                {
                    ErrorDetails errorDetails = toErrorDetails(importPreservationObjectResponse.ErrorInfo);
                    log.FormPipeErrorLog("Import preservation object", errorDetails);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                // Import metadata
                var importMetadataResponse = ImportMetadata(request);
                if (importMetadataResponse.ErrorInfo != null && importMetadataResponse.ErrorInfo.ErrorCode != 0)
                {
                    ErrorDetails errorDetails = toErrorDetails(importMetadataResponse.ErrorInfo);
                    log.FormPipeErrorLog("Import Metadata", errorDetails);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                // Apply import
                var applyImportResponse = ApplyImport(request);
                if (applyImportResponse.ErrorInfo != null && applyImportResponse.ErrorInfo.ErrorCode != 0)
                {
                    ErrorDetails errorDetails = toErrorDetails(applyImportResponse.ErrorInfo);
                    log.FormPipeErrorLog("Apply import", errorDetails);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, errorDetails.ErrorMessage);
                }

                returnSetId = applyImportResponse.ImportedFileSetId;
                System.Diagnostics.Debug.WriteLine("ApplyImport.ImportedFileSetId = " + applyImportResponse.ImportedFileSetId);
            }
            catch (Exception ex)
            {
                log.ErrorLog(errorID, ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }

            return Request.CreateResponse(HttpStatusCode.OK, new ImportResponse()
            {
                ImportedFileSetId = returnSetId
            });
        }

        private ImportPreservationObjectResponse ImportPreservationObject(ImportRequest importRequest)
        {
            var preservationObjectBytes = Convert.FromBase64String(importRequest.PreservationObject.Data);

            var request = new ImportPreservationObjectRequest
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                FileExtension = importRequest.PreservationObject.FileExtension,
                TotalFileSize = (long) preservationObjectBytes.Length,
                ChunkSize = preservationObjectBytes.Length,
                Chunk = preservationObjectBytes
            };

            return client.ImportPreservationObject(request);
        }

        private ImportMetadataFileResponse ImportMetadata(ImportRequest importRequest)
        {
            var metadataXmlBytes = Convert.FromBase64String(importRequest.MetadataXml);
            var request = new ImportMetadataFileRequest()
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                Encoding = "UTF-8",
                TotalFileSize = (long) metadataXmlBytes.Length,
                Chunk = metadataXmlBytes,
                ChunkSize = metadataXmlBytes.Length
            };

            return client.ImportMetadataFile(request);
        }

        private ApplyImportResponse ApplyImport(ImportRequest importRequest)
        {
            var metadataChecksum = CreateChecksum(Convert.FromBase64String(importRequest.MetadataXml));
            var fileChecksum = CreateChecksum(Convert.FromBase64String(importRequest.PreservationObject.Data));

            var request = new ApplyImportRequest()
            {
                SubmissionAgreementId = importRequest.SubmissionAgreementId,
                FileSetId = importRequest.Uuid,
                ImportMode = ImportMode.METADATAANDHASH_FS,
                MetadataChecksum = metadataChecksum,
                ConfidentialityLevel = importRequest.ConfidentialityLevel,
                ConfidentialityDegradationDate = importRequest.ConfidentialityDegradationDate,
                PersonalDataFlag = importRequest.PersonalDataFlag,
                Files = new System.Collections.Generic.List<FileInfo>
                {
                    new FileInfo
                    {
                        FileId = importRequest.Uuid,                // Guid.NewGuid().ToString() ?
                        OriginalFileId = Guid.NewGuid().ToString(),
                        OriginalFileName = importRequest.PreservationObject.FileName,
                        Checksum = fileChecksum
                    }
                }.ToArray()
            };

            return client.ApplyImport(request);
        }

        private static Checksum CreateChecksum(string input) => CreateChecksum(Encoding.UTF8.GetBytes(input));

        private static Checksum CreateChecksum(byte[] input)
        {
            MD5 hashString = new MD5CryptoServiceProvider();
            return new Checksum
            {
                Algorithm = "MD5",
                Value = hashString.ComputeHash(input)
            };
        }
        
    }
}
