﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Office.Interop.Excel;
using ExcelDna.Integration;

public class Test : IExcelAddIn
{
    public void AutoOpen()
    {
        ExcelIntegration.RegisterUnhandledExceptionHandler(ex => "!!!Error " + ex);

        ExcelAsyncUtil.CalculationCanceled += CalculationCanceled;

        // *** Normal approach - register for CalculationEnded event ***
        ExcelAsyncUtil.CalculationEnded += CalculationEnded;
        // *** End of normal approach ***

        //// *** Alternative approach - use Application.AfterCalculate ***
        //(ExcelDnaUtil.Application as Application).AfterCalculate += CalculationEnded;
        //// Must explicitly call CalculationEnded when Canceled to get same bahaviour as CalculationEnded event
        //ExcelAsyncUtil.CalculationCanceled += CalculationEnded;
        //// *** End of alternative approach ***

    }

    public void AutoClose()
    {
        ExcelAsyncUtil.CalculationCanceled -= CalculationCanceled;

        // *** Normal approach - unregister for CalculationEnded event ***
        ExcelAsyncUtil.CalculationEnded -= CalculationEnded;
        // *** End of normal approach ***

        //// *** Alternative approach - use Application.AfterCalculate ***
        //// Unregister the AfterCalculate event handler
        //(ExcelDnaUtil.Application as Application).AfterCalculate -= CalculationEnded;
        //// Unregister the CalculationCanceled event handler
        //ExcelAsyncUtil.CalculationCanceled -= CalculationEnded;
        //// *** End of alternative approach ***
    }


    // Cancellation support
    // We keep a CancellationTokenSource around, and set to a new one whenever a calculation has finished.
    static CancellationTokenSource _cancellation = new CancellationTokenSource();
    public static void CalculationCanceled()
    {
        Debug.Print("CalculationCanceled called");
        _cancellation.Cancel();
    }

    public static void CalculationEnded()
    {
        Debug.Print("CalculationEnded called");
        if (_cancellation.IsCancellationRequested)
        {
            // Reset the cancellation token source to allow new calculations to run
            _cancellation = new CancellationTokenSource();
        }
    }

    // Wrapper functions - exported to Excel
    public static void dnaEchoWithCancel(object valueToEcho, int msToDelay, ExcelAsyncHandle asyncHandle)
    {
        RunTask(EchoWithCancel(valueToEcho, msToDelay, _cancellation.Token), asyncHandle);
    }

    public static void dnaEchoWithCancelAwait(object valueToEcho, int msToDelay, ExcelAsyncHandle asyncHandle)
    {
        RunTask(EchoWithCancelAwait(valueToEcho, msToDelay, _cancellation.Token), asyncHandle);
    }

    public static void dnaEchoTaskHelper(object valueToEcho, int msToSleep, ExcelAsyncHandle asyncHandle)
    {
        RunTask(DelayedEcho(valueToEcho, msToSleep), asyncHandle);
    }

    // Actual implementations
    static Task<object> EchoWithCancel(object valueToEcho, int msToDelay, CancellationToken cancellationToken)
    {
        return Task.Delay(msToDelay, cancellationToken).ContinueWith(t => valueToEcho, TaskContinuationOptions.NotOnCanceled);
    }

    // Using Async.Await
    static async Task<object> EchoWithCancelAwait(object valueToEcho, int msToDelay, CancellationToken cancellationToken)
    {
        await Task.Delay(msToDelay, cancellationToken);
        return valueToEcho;
    }

    // Not cancelable
    static async Task<object> DelayedEcho(object valueToEcho, int msToDelay)
    {
        if (valueToEcho.Equals("Boom!")) throw new Exception("Boom! Boom! Boom!");
        await Task.Delay(msToDelay);
        return valueToEcho;
    }

    // Task helper - pass in running taks and async handle
    static void RunTask<TResult>(Task<TResult> task, ExcelAsyncHandle asyncHandle)
    {
        task.ContinueWith(t =>
        {
            try
            {
                // task.Result will throw an AggregateException if there was an error
                asyncHandle.SetResult(t.Result);
            }
            catch (AggregateException ex)
            {
                // There may be multiple exceptions...
                // Do we have to call Handle?
                asyncHandle.SetException(ex.InnerException);
            }

            // Unhandled exceptions here will crash Excel 
            // and leave open workbooks in an unrecoverable state...

        }, TaskContinuationOptions.NotOnCanceled);
    }
}