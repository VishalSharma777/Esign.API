namespace ESign.API.Utilities;


public class AppException : Exception
{
	public string Code { get; }      
	public int HttpStatus { get; }   

	public AppException(string code, string message, int httpStatus = 400)
		: base(message)             
	{
		Code = code;
		HttpStatus = httpStatus;
	}
}