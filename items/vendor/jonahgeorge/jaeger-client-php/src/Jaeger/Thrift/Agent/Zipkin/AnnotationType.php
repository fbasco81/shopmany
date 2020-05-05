<?php
namespace Jaeger\Thrift\Agent\Zipkin;

/**
 * Autogenerated by Thrift Compiler (0.11.0)
 *
 * DO NOT EDIT UNLESS YOU ARE SURE THAT YOU KNOW WHAT YOU ARE DOING
 *  @generated
 */
use Thrift\Base\TBase;
use Thrift\Type\TType;
use Thrift\Type\TMessageType;
use Thrift\Exception\TException;
use Thrift\Exception\TProtocolException;
use Thrift\Protocol\TProtocol;
use Thrift\Protocol\TBinaryProtocolAccelerated;
use Thrift\Exception\TApplicationException;


final class AnnotationType {
  const BOOL = 0;
  const BYTES = 1;
  const I16 = 2;
  const I32 = 3;
  const I64 = 4;
  const DOUBLE = 5;
  const STRING = 6;
  static public $__names = array(
    0 => 'BOOL',
    1 => 'BYTES',
    2 => 'I16',
    3 => 'I32',
    4 => 'I64',
    5 => 'DOUBLE',
    6 => 'STRING',
  );
}

